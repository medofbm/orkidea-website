using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orkideya.Application.Orders;
using Orkideya.Controllers;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;
using System.Text.Json;

internal static class CheckoutScenario
{
    public static async Task Run(ApplicationDbContext db, Action<bool, string> check)
    {
        var product = new Product { Name = "Synthetic checkout product" };
        var variant = new ProductVariant { Product = product, Size = "50 ml", Price = 30m };
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        var rate = await db.ShippingRates.FirstAsync();
        var cart = new List<CartItem> { new() { ProductVariantId = variant.ProductVariantId, ProductId = product.ProductId,
            ProductName = product.Name, Price = variant.Price, Quantity = 2 } };
        var session = new MemorySession();
        session.SetString("Cart", JsonSerializer.Serialize(cart));
        var controller = new CheckoutController(db,
            new WhatsAppNotificationService(new ConfigurationBuilder().Build(), NullLogger<WhatsAppNotificationService>.Instance), new OrderService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } } };
        var index = (ViewResult)await controller.Index();
        var model = (CheckoutViewModel)index.Model!;
        check(model.Input.CheckoutToken.HasValue && model.Input.CheckoutToken != Guid.Empty, "Checkout issues a server-side session-bound retry key");
        var input = model.Input;
        input.FirstName = "Synthetic"; input.LastName = "Customer";
        input.WhatsAppNumber = "+218000000000"; input.ShippingAddress = "Synthetic address"; input.ShippingRateId = rate.ShippingRateId;
        check(await controller.PlaceOrder(input) is RedirectToActionResult { ActionName: "Confirmation" }, "Checkout confirms a valid order");
        check(session.GetString("Cart") == null, "Successful checkout clears the submitted basket");
        var token = input.CheckoutToken;
        var stored = await db.Orders.AsNoTracking().SingleAsync(order => order.CheckoutToken == token);
        check(stored.TotalAmount == 60m + rate.Price, "Stored checkout total equals refreshed prices plus shipping");
        check(await controller.PlaceOrder(input) is RedirectToActionResult { ActionName: "Confirmation" }, "Repeated POST confirms the original order after the basket was cleared");
        check(await db.Orders.CountAsync(order => order.CheckoutToken == token) == 1, "Repeated controller submission creates no duplicate");
        session.SetString("Cart", JsonSerializer.Serialize(cart));
        await controller.PlaceOrder(input);
        check(session.GetString("Cart") != null, "Late retry preserves a new basket added after checkout");
        input.ShippingAddress = "Changed synthetic address";
        check(await controller.PlaceOrder(input) is ConflictObjectResult, "Controller rejects changed fields with an already-used retry key");
        input.CheckoutToken = Guid.NewGuid();
        check(await controller.PlaceOrder(input) is BadRequestObjectResult, "Controller rejects a key belonging to another checkout session");
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id => "synthetic-sql-session";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
