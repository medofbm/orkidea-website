using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orkideya.Models;

namespace Orkideya.Controllers;

public sealed record CheckoutReview([Required, StringLength(64, MinimumLength = 64)] string CartRevision, decimal ShippingFee);
public sealed record CheckoutApiOrder([Required] CheckoutInput Input, [Required] CheckoutReview Review);

public partial class CheckoutController
{
    [HttpGet("/api/v1/checkout")]
    public async Task<IActionResult> ApiCheckout() => CheckoutResponse(await Index());

    [HttpPost("/api/v1/checkout/shipping")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("shipping-preview")]
    public async Task<IActionResult> ApiShipping([FromBody] CheckoutInput input)
    {
        if (input is null) return BadRequest();
        return CheckoutResponse(await PreviewShipping(input));
    }

    [HttpPost("/api/v1/checkout/orders")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiOrder([FromBody] CheckoutApiOrder order)
    {
        if (order?.Input is null || order.Review is null ||
            order.Review.CartRevision?.Length != 64 || order.Review.ShippingFee < 0)
            return BadRequest(new { message = "بيانات مراجعة الطلب غير صالحة." });

        var result = await PlaceOrderCore(order.Input, order.Review);
        return result is RedirectToActionResult { ActionName: nameof(Confirmation) }
            ? await ApiReceipt()
            : CheckoutResponse(result);
    }

    [HttpGet("/api/v1/checkout/receipt")]
    public async Task<IActionResult> ApiReceipt()
    {
        if (await Confirmation() is not ViewResult { Model: CheckoutReceipt receipt }) return NotFound();
        return Ok(new
        {
            receipt.OrderId, receipt.CreatedAtUtc, receipt.Quantity, receipt.StatusLabel, receipt.PaymentMethod,
            productsTotal = Money(receipt.ProductsTotal), shippingFee = Money(receipt.ShippingCost),
            total = Money(receipt.TotalAmount)
        });
    }

    private IActionResult CheckoutResponse(IActionResult result)
    {
        if (result is RedirectToActionResult { ControllerName: "Cart" })
            return Conflict(new { message = "راجع السلة. قد يكون أحد الأحجام غير متاح أو الكمية غير صالحة." });
        if (result is not ViewResult { Model: CheckoutViewModel model }) return result;

        var fee = model.HomeDestinations is not null
            ? model.QuotedFee
            : model.ShippingRates.FirstOrDefault(rate => rate.ShippingRateId == model.Input.ShippingRateId)?.Price;
        var payload = new
        {
            model.Input.CheckoutToken, model.Input.QuoteToken,
            productsTotal = Money(model.CartTotal),
            shippingFee = fee.HasValue ? Money(fee.Value) : null,
            total = fee.HasValue ? Money(model.CartTotal + fee.Value) : null,
            cartRevision = CartRevision(model.CartItems),
            shippingRates = model.ShippingRates.Select(rate => new
            {
                id = rate.ShippingRateId, city = rate.CityName, price = Money(rate.Price),
                total = Money(model.CartTotal + rate.Price)
            }),
            destinations = model.HomeDestinations?.Select(pair => new
            {
                key = pair.Key, pair.Value.City, pair.Value.Area, pair.Value.Service, pair.Value.IsPickup
            }),
            errors = ModelState.Values.SelectMany(value => value.Errors).Select(error =>
                string.IsNullOrEmpty(error.ErrorMessage) ? "راجع بيانات الطلب وحاول مرة أخرى." : error.ErrorMessage).ToArray()
        };
        return ModelState.IsValid ? Ok(payload) : UnprocessableEntity(payload);
    }

    private static string CartRevision(IEnumerable<CartItem> cart) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(cart.OrderBy(item => item.ProductVariantId)
            .Select(item => new { item.ProductVariantId, item.Quantity, Price = Money(item.Price) }))));

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
