using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Inventory;
using Orkideya.Data;
using Orkideya.Models;

internal static class StockUnitScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        const string actor = "synthetic-unit-admin";
        var clock = TimeProvider.System;
        await using var db = open();
        var product = new Product { Name = "Synthetic unit oil" };
        var variant = new ProductVariant { Product = product, Size = "Synthetic size", Price = 5 };
        db.ProductVariants.Add(variant); await db.SaveChangesAsync();
        var svc = new StockService(db, clock);
        var id = product.ProductId;
        await svc.OpenAsync(id, "piece", actor);
        await svc.ConfigureAsync(id, variant.ProductVariantId, 3, 0, actor);
        await svc.AdjustAsync(id, 10, 1, Guid.NewGuid(), actor, "Synthetic opening");
        async Task<StockResult> Change(int account, string unit, int revision) { await using var c = open(); return await new StockService(c, clock).ChangeUnitAsync(account, unit, revision, actor); }
        async Task<StockAccount> Account() { await using var read = open(); return await read.StockAccounts.AsNoTracking().SingleAsync(s => s.ProductId == id); }

        check(await Change(id, "liter", 2) == StockResult.Invalid, "Unit change rejects an unknown unit");
        check(await Change(id + 100000, "ml", 0) == StockResult.Missing, "Unit change on a missing account is Missing");
        check(await Change(id, "ml", 1) == StockResult.Conflict, "Unit change with a stale revision is Conflict");
        check(await Change(id, "piece", 2) == StockResult.Unchanged, "Unit change to the same unit is Unchanged");

        // Open orders hold stock in the old unit, so a reservation blocks the change.
        await using (var orderDb = open())
        {
            var order = new Order { CustomerName = "Synthetic unit", WhatsAppNumber = "0000000000", City = "Test",
                ShippingAddress = "Test only", CheckoutToken = Guid.NewGuid(), CheckoutRequestHash = new string('A', 64),
                OrderDate = DateTime.UtcNow, ShippingCost = 1, TotalAmount = 6,
                Items = [new() { ProductId = id, UnitPrice = 5, Quantity = 1, VariantSize = "Saved label",
                    StockAccountId = id, StockUnitsPerItem = 3, StockUnit = "piece" }] };
            await new Orkideya.Application.Orders.OrderService(orderDb).PlaceAsync(order);
            check(await new StockService(orderDb, clock).ApplyOrderAsync(order.OrderId, 0, "Reserve", actor) == StockResult.Changed, "Unit scenario reserves stock for an open order");
        }
        var reserved = await Account();
        check(await Change(id, "ml", reserved.Revision) == StockResult.HasReservations, "Unit change is refused while stock is reserved");
        check((await Account()).Unit == "piece", "Refused unit change leaves the unit untouched");

        // Release the reservation through a direct ledger-safe update so the change can proceed.
        await using (var fix = open())
        {
            await fix.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockAccounts SET Reserved=0 WHERE ProductId={id}");
        }
        var before = await Account();
        check(await Change(id, "ml", before.Revision) == StockResult.Changed, "Unit change piece to ml succeeds");
        var after = await Account();
        await using var verify = open();
        check(after.Unit == "ml" && after.OnHand == 0 && after.Revision == before.Revision + 1, "Unit change zeroes the balance and bumps the revision");
        check(await verify.ProductVariants.AllAsync(v => v.ProductId != id || v.StockUnitsPerItem == null), "Unit change clears per-size consumption");
        check(await verify.StockMovements.AnyAsync(m => m.StockAccountId == id && m.Kind == "Unit" && m.OnHandDelta == -before.OnHand && m.ActorUserId == actor),
            "Unit change writes a Unit movement with the negated balance");
    }
}
