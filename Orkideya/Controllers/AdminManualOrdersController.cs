using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orkideya.Application.Orders;
using Orkideya.Data;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/orders/manual")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminManualOrdersController(ManualOrderService orders, ApplicationDbContext db,
    IDataProtectionProvider protection, ILogger<AdminManualOrdersController> logger) : ControllerBase
{
    public sealed record Confirmation(ManualOrderInput Input, [Required, StringLength(1024)] string ReviewToken);

    [HttpGet]
    public async Task<IActionResult> Options(CancellationToken ct)
    {
        var variants = await db.ProductVariants.AsNoTracking().Where(v => v.Price > 0)
            .OrderBy(v => v.Product.Name).ThenBy(v => v.Size).ThenBy(v => v.ProductVariantId)
            .Select(v => new { id = v.ProductVariantId, product = v.Product.Name, v.Size, v.Price }).ToListAsync(ct);
        var rates = await db.ShippingRates.AsNoTracking().Where(r => r.Price >= 0)
            .OrderBy(r => r.CityName).ThenBy(r => r.ShippingRateId)
            .Select(r => new { id = r.ShippingRateId, city = r.CityName, r.Price }).ToListAsync(ct);
        return Ok(new
        {
            variants = variants.Select(v => new { v.id, v.product, v.Size, price = Money(v.Price) }),
            rates = rates.Select(r => new { r.id, r.city, price = Money(r.Price) })
        });
    }

    [HttpPost("review"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ManualOrderInput input, CancellationToken ct)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } actor) return Forbid();
        var result = await orders.PreviewAsync(input, actor, ct);
        return result.Draft is { } draft
            ? Ok(ReviewResponse(draft))
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Confirmation confirmation, CancellationToken ct)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } actor) return Forbid();
        var input = confirmation.Input;
        if (input.RequestId is not { } requestId || requestId == Guid.Empty)
            return BadRequest(new { message = "راجع الطلب من جديد." });
        string fingerprint;
        try { fingerprint = Protector(actor, requestId).Unprotect(confirmation.ReviewToken); }
        catch (CryptographicException) { return Conflict(new { message = "المعاينة غير صالحة؛راجع الطلب من جديد." }); }

        try
        {
            var existing = await orders.FindOwnAsync(requestId, actor, ct);
            if (existing is not null)
            {
                if (existing.CheckoutRequestHash != fingerprint) throw new CheckoutConflictException();
                return Ok(new { orderId = existing.OrderId });
            }

            var latest = await orders.PreviewAsync(input, actor, ct);
            if (latest.Draft is not { } draft) return BadRequest(new { message = latest.Error });
            if (draft.Fingerprint != fingerprint)
                return Ok(ReviewResponse(draft, "تغيرت بيانات الطلب أو السعر. راجع المعاينة الجديدة قبل الحفظ."));

            var result = await orders.SaveAsync(draft, actor, ct);
            if (result.OrderId is { } id) return Ok(new { orderId = id });
            if (result.Draft is { } updated) return Ok(ReviewResponse(updated, result.Error));
            return BadRequest(new { message = result.Error });
        }
        catch (CheckoutConflictException)
        {
            return Conflict(new { message = "هذا الحفظ لا يطابق المعاينة. راجع الطلب قبل إعادة المحاولة." });
        }
        catch (Exception ex) when (ex is DbUpdateException or RetryLimitExceededException)
        {
            logger.LogError("Manual order API could not confirm save.");
            return StatusCode(503, new { message = "تعذر تأكيد الحفظ. أعد المحاولة من نفس المعاينة لتفادي التكرار." });
        }
    }

    private IDataProtector Protector(string actor, Guid requestId) =>
        protection.CreateProtector("Orkidea.AdminManualOrders.Review.v1", actor, requestId.ToString("D"));

    private object ReviewResponse(ManualOrderDraft draft, string? message = null) => new
    {
        reviewToken = Protector(draft.ActorId, draft.RequestId).Protect(draft.Fingerprint),
        message,
        draft.City,
        shippingCost = Money(draft.ShippingCost),
        productsTotal = Money(draft.ProductsTotal),
        total = Money(draft.Total),
        lines = draft.Lines.Select(line => new { line.ProductName, line.Size, line.Quantity, unitPrice = Money(line.UnitPrice) })
    };

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
