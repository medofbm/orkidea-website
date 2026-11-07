using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Delivery;
using Orkideya.Data;

namespace Orkideya.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class DeliveryController(ApplicationDbContext db, DeliveryService service, ILogger<DeliveryController> logger) : Controller
{
    public async Task<IActionResult> Index(string state = "active", string search = "", int page = 1)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await new DeliveryListQuery(db).PageAsync(state, search, page, HttpContext.RequestAborted);
        return result is null ? BadRequest() : View(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int id, DeliveryInput input)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();
        if (!ModelState.IsValid) TempData["DeliveryNotice"] = "راجع حقول الشحنة والسبب وأعد المحاولة من الصفحة الحالية.";
        else
        {
            try
            {
                var result = await service.ApplyAsync(id, input, actor, HttpContext.RequestAborted);
                TempData["DeliveryNotice"] = result switch
                {
                    DeliveryResult.Changed => "تم حفظ إجراء التوصيل وسجله.",
                    DeliveryResult.Unchanged => "الإجراء محفوظ مسبقاً؛ لم يتكرر.",
                    DeliveryResult.Conflict => "تغيرت البيانات أو استعمل مرجع العملية من قبل. راجع الحالة الحالية قبل إعادة المحاولة.",
                    DeliveryResult.Missing => "الطلب غير موجود.",
                    DeliveryResult.RequiresPreparation => "أكد تجهيز الطلب وبنوده وتعبئته قبل التسليم. الطلب القديم غير المتتبع يحتاج تأكيد مراجعتك يدوياً.",
                    _ => "الإجراء لا يناسب حالة الشحنة، أو ينقصه سبب أو تأكيد الاستلام/سلامة نفس الطلبية. راجع الحقول والحالة الحالية."
                };
            }
            catch (DbUpdateException)
            {
                logger.LogError("Delivery operation could not be saved for order {OrderId}", id);
                TempData["DeliveryNotice"] = "تعذر حفظ الإجراء. راجع الحالة والسجل قبل إعادة المحاولة.";
            }
        }
        return RedirectToAction("Details", "Orders", new { id });
    }
}
