using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Delivery;
using Orkideya.Areas.Admin.Models;
using Orkideya.Data;

namespace Orkideya.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class DeliveryController(ApplicationDbContext db, DeliveryService service, ILogger<DeliveryController> logger) : Controller
{
    public async Task<IActionResult> Index(string state = "active", string search = "", int page = 1)
    {
        var states = new[] { "active", "all", "HandedOver", "Returning", "Returned", "Delivered" };
        var canonical = states.FirstOrDefault(s => s.Equals(state?.Trim(), StringComparison.OrdinalIgnoreCase));
        search = search?.Trim() ?? "";
        if (!ModelState.IsValid || canonical is null || search.Length > 100 || page < 1) return BadRequest();
        var query = from d in db.OrderDeliveries.AsNoTracking()
                    join o in db.Orders.AsNoTracking() on d.OrderId equals o.OrderId
                    select new DeliveryListRow { OrderId = o.OrderId, CustomerName = o.CustomerName, City = o.City,
                        Mode = d.Mode, CarrierName = d.CarrierName, ShipmentReference = d.ShipmentReference,
                        State = d.State, UpdatedAtUtc = d.UpdatedAtUtc };
        if (canonical == "active") query = query.Where(d => d.State == "HandedOver" || d.State == "Returning");
        else if (canonical != "all") query = query.Where(d => d.State == canonical);
        if (search.Length != 0)
        {
            var number = search.StartsWith('#') ? search[1..] : search;
            var id = int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            query = query.Where(d => d.OrderId == id || d.CustomerName.Contains(search) || d.City.Contains(search)
                || d.CarrierName != null && d.CarrierName.Contains(search)
                || d.ShipmentReference != null && d.ShipmentReference.Contains(search));
        }
        var total = await query.CountAsync(HttpContext.RequestAborted);
        var pages = (int)Math.Max(1L, (total + 24L) / 25);
        page = Math.Min(page, pages);
        var rows = await query.OrderByDescending(d => d.UpdatedAtUtc).ThenByDescending(d => d.OrderId)
            .Skip((page - 1) * 25).Take(25).ToListAsync(HttpContext.RequestAborted);
        return View(new DeliveryIndexViewModel { Rows = rows, State = canonical, Search = search, Page = page, Pages = pages, Total = total });
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
