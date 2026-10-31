using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Inventory;
using Orkideya.Application.Orders;
using Orkideya.Areas.Admin.Models;
using Orkideya.Data;

namespace Orkideya.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class InventoryController(ApplicationDbContext db, StockService stock, OrderStatusService statuses, ILogger<InventoryController> logger) : Controller
{
    public async Task<IActionResult> Index() => View(new StockIndexViewModel
    {
        Products = await db.Products.AsNoTracking().Include(p => p.ProductVariants).OrderBy(p => p.ProductId).ToListAsync(),
        Accounts = await db.StockAccounts.AsNoTracking().ToDictionaryAsync(s => s.ProductId)
    });

    public async Task<IActionResult> Details(int id, int page = 1)
    {
        if (page is < 1 or > 100000) return BadRequest();
        var product = await db.Products.AsNoTracking().Include(p => p.ProductVariants).SingleOrDefaultAsync(p => p.ProductId == id);
        var account = await db.StockAccounts.AsNoTracking().SingleOrDefaultAsync(s => s.ProductId == id);
        if (product is null || account is null) return NotFound();
        var query = db.StockMovements.AsNoTracking().Where(m => m.StockAccountId == id);
        var total = await query.CountAsync();
        var moves = await query.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.StockMovementId).Skip((page-1)*50).Take(50).ToListAsync();
        var ids = moves.Select(m => m.ActorUserId).Distinct().ToArray();
        return View(new StockDetailsViewModel { Product = product, Account = account, Movements = moves, Page = page, TotalMovements = total,
            Actors = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? "حساب إداري") });
    }

    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private void Notice(StockResult result) => TempData["StockNotice"] = result switch
    {
        StockResult.Changed => "تم حفظ العملية وسجلها.", StockResult.Unchanged => "العملية محفوظة مسبقاً؛ لم تكرر الكمية.",
        StockResult.Conflict => "تغيّرت البيانات من صفحة أخرى. راجع الرصيد الحالي قبل إعادة المحاولة.",
        StockResult.Insufficient => "الكمية المتاحة لا تكفي، أو أن التصحيح يمس كمية محجوزة. راجع الرصيد.",
        StockResult.Missing => "المنتج أو الطلب غير موجود.", _ => "بيانات العملية غير صالحة أو لا تناسب حالة الطلب. راجع الحقول والتجهيز."
    };
    private async Task Run(Func<Task<StockResult>> action)
    {
        try { Notice(await action()); }
        catch (DbUpdateException) { logger.LogError("Stock operation could not be saved"); TempData["StockNotice"] = "تعذر الحفظ. راجع البيانات الحالية قبل إعادة المحاولة."; }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(int id, string unit)
    {
        if (!StockService.ValidActor(Actor)) return Forbid();
        await Run(() => stock.OpenAsync(id, unit, Actor, HttpContext.RequestAborted));
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(int id, StockVariantInput input)
    {
        if (!StockService.ValidActor(Actor)) return Forbid();
        if (!ModelState.IsValid) Notice(StockResult.Invalid);
        else await Run(() => stock.ConfigureAsync(id, input.VariantId, input.Units!.Value, input.ExpectedRevision!.Value, Actor, HttpContext.RequestAborted));
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int id, StockAdjustmentInput input)
    {
        if (!StockService.ValidActor(Actor)) return Forbid();
        if (!ModelState.IsValid) Notice(StockResult.Invalid);
        else await Run(() => stock.AdjustAsync(id, input.Delta!.Value, input.ExpectedRevision!.Value, input.RequestId!.Value, Actor, input.Reason, HttpContext.RequestAborted));
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OrderOperation(int id, int? expectedRevision, string operation)
    {
        if (!StockService.ValidActor(Actor)) return Forbid();
        if (!ModelState.IsValid || expectedRevision is null) Notice(StockResult.Invalid);
        else await Run(() => stock.ApplyOrderAsync(id, expectedRevision.Value, operation, Actor, HttpContext.RequestAborted));
        return RedirectToAction("Details", "Orders", new { id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, int? expectedRevision, string reason)
    {
        if (!StockService.ValidActor(Actor)) return Forbid();
        if (!ModelState.IsValid || expectedRevision is null || string.IsNullOrWhiteSpace(reason) || reason.Length > 500) Notice(StockResult.Invalid);
        else
        {
            try
            {
                var result = await statuses.ChangeAsync(id, "Cancelled", expectedRevision, Actor, HttpContext.RequestAborted, reason);
                Notice(result switch { StatusChangeResult.Changed => StockResult.Changed, StatusChangeResult.Unchanged => StockResult.Unchanged,
                    StatusChangeResult.Conflict => StockResult.Conflict, _ => StockResult.Invalid });
            }
            catch (DbUpdateException) { logger.LogError("Stock cancellation could not be saved"); TempData["StockNotice"] = "تعذر حفظ الإلغاء. راجع الطلب قبل إعادة المحاولة."; }
        }
        return RedirectToAction("Details", "Orders", new { id });
    }
}
