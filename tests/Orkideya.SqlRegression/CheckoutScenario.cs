using Orkideya.Application.Inventory;
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
        var controller = new CheckoutController(db, new OrderService(db))
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
        var inventory = new StockService(db, TimeProvider.System);
        check(await inventory.OpenAsync(product.ProductId,"ml","synthetic-checkout-admin") == StockResult.Changed &&
            await inventory.ConfigureAsync(product.ProductId,variant.ProductVariantId,50,0,"synthetic-checkout-admin") == StockResult.Changed &&
            await inventory.AdjustAsync(product.ProductId,1000,1,Guid.NewGuid(),"synthetic-checkout-admin","Synthetic oil") == StockResult.Changed,
            "Checkout stock fixture is explicitly configured without inferring size labels");
        CheckoutInput Customer(CheckoutViewModel view) => new() { CheckoutToken=view.Input.CheckoutToken, FirstName="Synthetic", LastName="Stock",
            WhatsAppNumber="+218000000000", ShippingAddress="Test only", ShippingRateId=rate.ShippingRateId };
        controller.ModelState.Clear(); session.SetString("Cart",JsonSerializer.Serialize(cart));
        var trackedView=(CheckoutViewModel)((ViewResult)await controller.Index()).Model!;
        var trackedInput=Customer(trackedView);
        check(await controller.PlaceOrder(trackedInput) is RedirectToActionResult {ActionName:"Confirmation"},"Tracked checkout saves a valid order");
        var tracked=await db.Orders.AsNoTracking().Include(o=>o.Items).SingleAsync(o=>o.CheckoutToken==trackedInput.CheckoutToken);
        var stock=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==product.ProductId);
        check(tracked.StockState=="AwaitingConfirmation" && tracked.Items.Single().StockAccountId==product.ProductId &&
            tracked.Items.Single().StockUnitsPerItem==50 && tracked.Items.Single().StockUnit=="ml" && stock.Reserved==0 && stock.OnHand==1000,
            "Checkout snapshots configured consumption and unit but does not reserve or consume");
        check(await controller.PlaceOrder(trackedInput) is RedirectToActionResult {ActionName:"Confirmation"} &&
            !await db.StockMovements.AnyAsync(m=>m.OrderId==tracked.OrderId),"Checkout retry does not create stock movements");
        var missingProduct=new Product{Name="Synthetic missing stock size"};
        var missingVariant=new ProductVariant{Product=missingProduct,Size="Unknown size",Price=10};
        db.ProductVariants.Add(missingVariant); await db.SaveChangesAsync();
        await inventory.OpenAsync(missingProduct.ProductId,"ml","synthetic-checkout-admin");
        var before=await db.Orders.CountAsync();
        var missingCart=new List<CartItem>{new(){ProductVariantId=missingVariant.ProductVariantId,ProductId=missingProduct.ProductId,ProductName=missingProduct.Name,Price=10,Quantity=1}};
        controller.ModelState.Clear(); session.SetString("Cart",JsonSerializer.Serialize(missingCart));
        var missingView=(CheckoutViewModel)((ViewResult)await controller.Index()).Model!;
        check(await controller.PlaceOrder(Customer(missingView)) is ViewResult && await db.Orders.CountAsync()==before && session.GetString("Cart")!=null,
            "Enabled stock with unconfigured size fails closed and preserves basket without creating an order");
        var untrackedProduct=new Product{Name="Synthetic untracked product"};
        var untrackedVariant=new ProductVariant{Product=untrackedProduct,Size="Test",Price=2};
        db.ProductVariants.Add(untrackedVariant); await db.SaveChangesAsync();
        var mixed=new List<CartItem>{cart[0],new(){ProductVariantId=untrackedVariant.ProductVariantId,ProductId=untrackedProduct.ProductId,ProductName=untrackedProduct.Name,Price=2,Quantity=1}};
        controller.ModelState.Clear(); session.SetString("Cart",JsonSerializer.Serialize(mixed));
        var mixedView=(CheckoutViewModel)((ViewResult)await controller.Index()).Model!;
        check(await controller.PlaceOrder(Customer(mixedView)) is ViewResult && await db.Orders.CountAsync()==before,
            "Mixed configured and untracked products cannot bypass reservation through an untracked order");

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
