using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Orders;
using Orkideya.Controllers;
using Orkideya.Data;
using Orkideya.Models;
using System.Text.Json;

/// <summary>Free-delivery promotion against a real SQL Server: website checkout charges and stores 0.</summary>
internal static class DeliveryPromotionScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        await using var db = open();
        var product = new Product { Name = "Synthetic promotion product" };
        var variant = new ProductVariant { Product = product, Size = "50 ml", Price = 40m };
        db.ProductVariants.Add(variant);
        var tripoli = new ShippingRate { CityName = "طرابلس — منطقة تجربة العرض", Region = "طرابلس", Price = 12m };
        var misrata = new ShippingRate { CityName = "مصراتة — منطقة تجربة العرض", Region = "مصراتة", Price = 20m };
        db.ShippingRates.AddRange(tripoli, misrata);
        db.DeliveryPromotions.Add(new DeliveryPromotion
        {
            Title = "عرض الجمعة البيضاء", IsEnabled = true, Scope = DeliveryPromotion.ScopeTripoli,
            StartsAtUtc = DateTime.UtcNow.AddHours(-1), EndsAtUtc = DateTime.UtcNow.AddHours(5),
            UpdatedAtUtc = DateTime.UtcNow, UpdatedBy = "synthetic"
        });
        await db.SaveChangesAsync();

        async Task<(CheckoutController Controller, MemorySession Session, CheckoutInput Input)> Start()
        {
            var session = new MemorySession();
            session.SetString("Cart", JsonSerializer.Serialize(new List<CartItem> { new() { ProductVariantId = variant.ProductVariantId,
                ProductId = product.ProductId, ProductName = product.Name, Price = variant.Price, Quantity = 1 } }));
            var controller = new CheckoutController(db, new OrderService(db))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } } };
            var model = (CheckoutViewModel)((ViewResult)await controller.Index()).Model!;
            var input = model.Input;
            input.FirstName = "Synthetic"; input.LastName = "Promo"; input.WhatsAppNumber = "+218000000000";
            input.ShippingAddress = "Synthetic address";
            return (controller, session, input);
        }

        var (inside, insideSession, insideInput) = await Start();
        insideInput.ShippingRateId = tripoli.ShippingRateId;
        var preview = await inside.ApiCheckout();
        var json = JsonSerializer.Serialize((preview as OkObjectResult)?.Value);
        check(json.Contains("\"originalPrice\":\"12.00\"") && json.Contains("\"originalPrice\":null"),
            "Checkout API marks Tripoli rates free with the normal price and leaves other cities unchanged");
        var placed = await inside.ApiOrder(new CheckoutApiOrder(insideInput, new CheckoutReview(Revision(insideSession), 0m)));
        var stored = await db.Orders.AsNoTracking().SingleAsync(o => o.CheckoutToken == insideInput.CheckoutToken);
        check(placed is OkObjectResult && stored.ShippingCost == 0m && stored.OriginalShippingCost == 12m && stored.TotalAmount == 40m,
            "Website order inside the promotion stores a charged fee of 0 and the normal fee beside it");
        var receiptJson = JsonSerializer.Serialize(((OkObjectResult)placed).Value);
        check(receiptJson.Contains("\"shippingFee\":\"0.00\"") && receiptJson.Contains("\"originalShippingFee\":\"12.00\""),
            "Receipt shows the free delivery and the normal fee");

        var (stale, staleSession, staleInput) = await Start();
        staleInput.ShippingRateId = tripoli.ShippingRateId;
        check(await stale.ApiOrder(new CheckoutApiOrder(staleInput, new CheckoutReview(Revision(staleSession), 12m))) is not OkObjectResult &&
            !await db.Orders.AnyAsync(o => o.CheckoutToken == staleInput.CheckoutToken),
            "A client review with the normal fee is rejected while the promotion makes it free");

        var (outside, outsideSession, outsideInput) = await Start();
        outsideInput.ShippingRateId = misrata.ShippingRateId;
        await outside.ApiOrder(new CheckoutApiOrder(outsideInput, new CheckoutReview(Revision(outsideSession), 20m)));
        var other = await db.Orders.AsNoTracking().SingleAsync(o => o.CheckoutToken == outsideInput.CheckoutToken);
        check(other.ShippingCost == 20m && other.OriginalShippingCost is null, "Orders outside the promotion scope pay the normal fee");

        var bad = await db.Orders.AsNoTracking().FirstAsync(o => o.OrderId == other.OrderId);
        var constraintHeld = false;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Orders SET OriginalShippingCost = 5 WHERE OrderId = {bad.OrderId}");
        }
        catch (Microsoft.Data.SqlClient.SqlException) { constraintHeld = true; }
        check(constraintHeld, "Database refuses a promotion fee record on an order that was charged delivery");

        db.DeliveryPromotions.RemoveRange(db.DeliveryPromotions);
        await db.SaveChangesAsync();
    }

    private static string Revision(MemorySession session)
    {
        var cart = JsonSerializer.Deserialize<List<CartItem>>(session.GetString("Cart")!)!;
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            cart.OrderBy(item => item.ProductVariantId).Select(item => new { item.ProductVariantId, item.Quantity,
                Price = item.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }))));
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id => "synthetic-promotion-session";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
