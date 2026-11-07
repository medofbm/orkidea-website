using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Delivery;
using Orkideya.Application.Inventory;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/orders/{id:int}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ValidateAntiForgeryToken]
public sealed class AdminFulfillmentController(StockService stock, DeliveryService delivery,
    ILogger<AdminFulfillmentController> logger) : ControllerBase
{
    public sealed record StockInput(
        [Required, Range(0, int.MaxValue)] int? ExpectedRevision,
        [Required, StringLength(16)] string Operation);

    [HttpPost("stock")]
    public async Task<IActionResult> UpdateStock(int id, StockInput input, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null || !StockService.ValidActor(actor)) return Forbid();

        try
        {
            var result = await stock.ApplyOrderAsync(id, input.ExpectedRevision!.Value,
                input.Operation, actor, cancellationToken);
            return result switch
            {
                StockResult.Changed => Ok(new { success = true, message = "تم حفظ حركة المخزون." }),
                StockResult.Unchanged => Ok(new { success = true, message = "الحركة محفوظة مسبقاً." }),
                StockResult.Missing => NotFound(new { message = "الطلب غير موجود." }),
                StockResult.Conflict => Conflict(new { message = "تغيّر الطلب أو الرصيد. حدّث التفاصيل وراجع الكمية." }),
                StockResult.Insufficient => BadRequest(new { message = "كمية المخزون المتاحة لا تكفي لهذه العملية." }),
                _ => BadRequest(new { message = "الحركة لا تناسب حالة الطلب أو إعداد المخزون." })
            };
        }
        catch (DbUpdateException)
        {
            logger.LogError("Order stock update failed for order {OrderId}", id);
            return StatusCode(500, new { message = "تعذر حفظ حركة المخزون. حدّث تفاصيل الطلب للتحقق." });
        }
    }

    [HttpPost("delivery")]
    public async Task<IActionResult> UpdateDelivery(int id, DeliveryInput input, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Forbid();

        try
        {
            var result = await delivery.ApplyAsync(id, input, actor, cancellationToken);
            return result switch
            {
                DeliveryResult.Changed => Ok(new { success = true, message = "تم حفظ إجراء التوصيل وسجله." }),
                DeliveryResult.Unchanged => Ok(new { success = true, message = "إجراء التوصيل محفوظ مسبقاً." }),
                DeliveryResult.Missing => NotFound(new { message = "الطلب غير موجود." }),
                DeliveryResult.Conflict => Conflict(new { message = "تغيّرت بيانات الطلب. حدّث التفاصيل قبل إعادة المحاولة." }),
                DeliveryResult.RequiresPreparation => BadRequest(new { message = "أكد تجهيز الطلب وتعبئته قبل تسليمه للتوصيل." }),
                _ => BadRequest(new { message = "الإجراء لا يناسب حالة الشحنة أو تنقصه بيانات التأكيد." })
            };
        }
        catch (DbUpdateException)
        {
            logger.LogError("Order delivery update failed for order {OrderId}", id);
            return StatusCode(500, new { message = "تعذر حفظ إجراء التوصيل. حدّث تفاصيل الطلب للتحقق." });
        }
    }
}
