using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Orkideya.Controllers;

public sealed record CartApiInput([Range(1, int.MaxValue)] int VariantId, [Range(1, 99)] int Quantity);

public partial class CartController
{
    [HttpGet("/api/v1/session")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ApiSession([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Json(new { requestToken = tokens.RequestToken });
    }

    [HttpGet("/api/v1/cart")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ApiCart(CancellationToken cancellationToken)
    {
        var cart = GetCart();
        var ids = cart.Select(item => item.ProductVariantId).ToArray();
        var variants = await _context.ProductVariants.AsNoTracking().Include(variant => variant.Product)
            .Where(variant => ids.Contains(variant.ProductVariantId))
            .ToDictionaryAsync(variant => variant.ProductVariantId, cancellationToken);
        var items = cart.Select(item =>
        {
            variants.TryGetValue(item.ProductVariantId, out var variant);
            var available = variant?.Product is not null && variant.Price > 0;
            var price = available ? variant!.Price : item.Price;
            return new
            {
                variantId = item.ProductVariantId, productId = item.ProductId,
                name = variant?.Product?.Name ?? item.ProductName, size = variant?.Size,
                image = variant?.Product?.ImageUrl ?? item.ImageUrl, quantity = item.Quantity,
                price = price.ToString("0.00", CultureInfo.InvariantCulture),
                subtotal = (price * item.Quantity).ToString("0.00", CultureInfo.InvariantCulture),
                available
            };
        }).ToArray();
        var total = items.Sum(item => decimal.Parse(item.subtotal, CultureInfo.InvariantCulture));
        return Json(new { items, count = items.Sum(item => item.quantity),
            productsTotal = total.ToString("0.00", CultureInfo.InvariantCulture), currency = "LYD" });
    }

    [HttpPost("/api/v1/cart/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiAdd([FromBody] CartApiInput input)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "الكمية المطلوبة يجب أن تكون بين 1 و99." });
        return await AddToCartAjax(input.VariantId, input.Quantity);
    }

    [HttpPatch("/api/v1/cart/items/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult ApiQuantity(int id, [FromBody] CartApiInput input)
    {
        if (!ModelState.IsValid || id != input.VariantId)
            return BadRequest(new { message = "بيانات الكمية غير صالحة." });
        var cart = GetCart();
        var item = cart.SingleOrDefault(item => item.ProductVariantId == id);
        if (item is null) return NotFound(new { message = "الحجم غير موجود في السلة." });
        item.Quantity = input.Quantity;
        SaveCart(cart);
        return Json(new { success = true });
    }

    [HttpDelete("/api/v1/cart/items/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult ApiRemove(int id)
    {
        var cart = GetCart();
        cart.RemoveAll(item => item.ProductVariantId == id);
        SaveCart(cart);
        return Json(new { success = true });
    }
}
