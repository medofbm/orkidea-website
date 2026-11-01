using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orkideya.Application.Orders;
using Orkideya.Data;

namespace Orkideya.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class ManualOrdersController(ManualOrderService service, ApplicationDbContext db, ILogger<ManualOrdersController> logger) : Controller
{
    private const string DraftKey = "AdminFacebookOrderDraft";
    private string? Actor => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private ManualOrderDraft? Draft()
    {
        var json = HttpContext.Session.GetString(DraftKey);
        try { return json is null ? null : JsonSerializer.Deserialize<ManualOrderDraft>(json); }
        catch (JsonException) { HttpContext.Session.Remove(DraftKey); return null; }
    }
    private void Remember(ManualOrderDraft draft) => HttpContext.Session.SetString(DraftKey, JsonSerializer.Serialize(draft));
    private async Task<IActionResult> Form(ManualOrderInput input)
    {
        var variants = await db.ProductVariants.AsNoTracking().Include(v => v.Product).OrderBy(v => v.Product.Name).ThenBy(v => v.Size).ToListAsync();
        ViewBag.Variants = variants.Select(v => new SelectListItem($"{v.Product.Name} — {v.Size} — {v.Price:0.00} د.ل", v.ProductVariantId.ToString())).ToList();
        ViewBag.Rates = await db.ShippingRates.AsNoTracking().Where(r => r.Price >= 0).OrderBy(r => r.CityName)
            .Select(r => new SelectListItem(r.CityName + " — " + r.Price + " د.ل", r.ShippingRateId.ToString())).ToListAsync();
        return View("Create", input);
    }
    [HttpGet]
    public Task<IActionResult> Create(Guid? edit)
    {
        var draft = Draft();
        return Form(edit is not null && draft?.RequestId == edit && draft.ActorId == Actor ? draft.Input :
            new ManualOrderInput { RequestId = Guid.NewGuid(), Lines = [new()] });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] ManualOrderInput input, string command = "review")
    {
        if (Actor is not { } actor) return Forbid();
        if (input.Lines is null || input.Lines.Count is < 1 or > 30 || input.Lines.Any(l => l is null)) return BadRequest();
        if (command == "add" || command.StartsWith("remove:", StringComparison.Ordinal))
        {
            if (command == "add" && input.Lines.Count < 30) input.Lines.Add(new());
            else if (command.StartsWith("remove:", StringComparison.Ordinal) && int.TryParse(command[7..], out var index) && index >= 0 && index < input.Lines.Count && input.Lines.Count > 1)
                input.Lines.RemoveAt(index);
            else return BadRequest("عدد البنود من 1 إلى 30؛راجع الصفوف.");
            // Reindex names after removing a row while keeping every entered value.
            ModelState.Clear();
            return await Form(input);
        }
        if (command != "review") return BadRequest();
        if (!ModelState.IsValid) return await Form(input);
        var preview = await service.PreviewAsync(input, actor, HttpContext.RequestAborted);
        if (preview.Draft is null) { ModelState.AddModelError("", preview.Error!); return await Form(input); }
        Remember(preview.Draft);
        return View("Review", preview.Draft);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid requestId, string? fingerprint)
    {
        if (Actor is not { } actor) return Forbid();
        if (!ModelState.IsValid || requestId == Guid.Empty || fingerprint?.Length != 64) return BadRequest();
        try
        {
            var existing = await service.FindOwnAsync(requestId, actor, HttpContext.RequestAborted);
            if (existing is not null)
            {
                if (existing.CheckoutRequestHash != fingerprint) throw new CheckoutConflictException();
                return RedirectToAction("Details", "Orders", new { id = existing.OrderId });
            }
            var draft = Draft();
            if (draft is null || draft.RequestId != requestId || draft.ActorId != actor)
            {
                Response.StatusCode = 409;
                ModelState.AddModelError("", "المعاينة غير متاحة أو فتحت معاينة أخرى؛راجع الطلب قبل الحفظ.");
                return await Form(new ManualOrderInput { RequestId = Guid.NewGuid(), Lines = [new()] });
            }
            if (draft.Fingerprint != fingerprint)
            {
                Response.StatusCode = 409;
                ViewBag.ReviewWarning = "هذه صفحة معاينة قديمة. راجع البيانات الحالية أدناه قبل الحفظ.";
                return View("Review", draft);
            }
            var result = await service.SaveAsync(draft, actor, HttpContext.RequestAborted);
            if (result.OrderId is { } id)
            {
                // Do not clear a newer draft or the customer's independent shopping basket.
                if (Draft()?.RequestId == requestId) HttpContext.Session.Remove(DraftKey);
                TempData["SuccessMessage"] = "تم تسجيل الطلب الجديد. راجعه وأكده لحجز المخزون؛لم يُحجز أو يُشحن تلقائياً.";
                return RedirectToAction("Details", "Orders", new { id });
            }
            if (result.Draft is { } changed) { Remember(changed); ViewBag.ReviewWarning = result.Error; return View("Review", changed); }
            ModelState.AddModelError("", result.Error!); return await Form(draft.Input);
        }
        catch (CheckoutConflictException) { return Conflict("هذا الحفظ لا يطابق المعاينة أو المستخدم. افتح تفاصيل الطلب أو أعد مراجعته."); }
        catch (Exception ex) when (ex is DbUpdateException or RetryLimitExceededException)
        {
            logger.LogError("Manual order save could not be confirmed; no customer data logged.");
            var draft = Draft(); Response.StatusCode = 503;
            if (draft?.RequestId == requestId && draft.ActorId == actor)
            { ViewBag.ReviewWarning = "تعذر تأكيد الحفظ. أعد المحاولة من هذه المعاينة؛المفتاح نفسه يمنع إنشاء نسخة مكررة."; return View("Review", draft); }
            return StatusCode(503, "تعذر تأكيد الحفظ؛حدّث قائمة الطلبات للتحقق.");
        }
    }
}
