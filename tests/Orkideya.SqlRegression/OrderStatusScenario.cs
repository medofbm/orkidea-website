using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

internal static class OrderStatusScenario
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2043, 2, 3, 4, 5, 6, TimeSpan.Zero);
    }
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        var clock = new FixedClock();
        int id;
        await using (var db = open())
        {
            var order = new Order { CustomerName = "Synthetic history", WhatsAppNumber = "0000000000",
                City = "Synthetic", ShippingAddress = "Test", TotalAmount = 3, ShippingCost = 1,
                OrderDate = clock.GetUtcNow().UtcDateTime };
            db.Orders.Add(order); await db.SaveChangesAsync(); id = order.OrderId;
            check(!await db.OrderStatusChanges.AnyAsync(change => change.OrderId == id), "History does not invent earlier changes");
            var service = new OrderStatusService(db, clock);
            check(await service.ChangeAsync(id, "processing", 0, "synthetic-admin") == StatusChangeResult.Changed,
                "Admin status change commits with canonical status");
            var history = await db.OrderStatusChanges.SingleAsync(change => change.OrderId == id);
            check(order.Status == "Processing" && order.StatusRevision == 1 && history.Revision == 1 &&
                history.PreviousStatus == "Pending" && history.NewStatus == "Processing" &&
                history.ActorUserId == "synthetic-admin" && history.ChangedAtUtc == clock.GetUtcNow().UtcDateTime,
                "History records exact before/after, revision, authenticated actor and UTC time");
            check(await service.ChangeAsync(id, "Processing", 1, "synthetic-admin") == StatusChangeResult.Unchanged &&
                await db.OrderStatusChanges.CountAsync(change => change.OrderId == id) == 1,
                "Current-state replay creates no duplicate history");
            check(await service.ChangeAsync(id, "Delivered", 0, "synthetic-admin") == StatusChangeResult.Conflict,
                "Stale page cannot overwrite a later status");
            check(await service.ChangeAsync(id, "BadStatus", 1, "synthetic-admin") == StatusChangeResult.Invalid &&
                await service.ChangeAsync(id, "Shipped", null, "synthetic-admin") == StatusChangeResult.Invalid &&
                await service.ChangeAsync(id, "Shipped", 1, "") == StatusChangeResult.Invalid,
                "Invalid status, missing revision and missing actor are rejected");
            check(await service.ChangeAsync(int.MaxValue, "Shipped", 0, "synthetic-admin") == StatusChangeResult.Missing,
                "Missing order is not mutated");
        }
        // Force an audit insert failure and prove the order update rolls back with it.
        await using (var db = open()) await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE OrderStatusChanges ADD CONSTRAINT CK_StatusHistorySyntheticFailure CHECK (NewStatus <> N'Cancelled')");
        try
        {
            await using var db = open();
            var failed = false;
            try { await new OrderStatusService(db, clock).ChangeAsync(id, "Cancelled", 1, "synthetic-admin"); }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 }) { failed = true; }
            check(failed, "History persistence failure reaches caller");
            await using var verify = open();
            var order = await verify.Orders.SingleAsync(order => order.OrderId == id);
            check(order.Status == "Processing" && order.StatusRevision == 1 &&
                await verify.OrderStatusChanges.CountAsync(change => change.OrderId == id) == 1,
                "Audit failure rolls back status and revision atomically");
        }
        finally
        {
            await using var db = open();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderStatusChanges DROP CONSTRAINT CK_StatusHistorySyntheticFailure");
        }
        await using (var first = open())
        await using (var second = open())
        {
            await first.Orders.SingleAsync(order => order.OrderId == id);
            await second.Orders.SingleAsync(order => order.OrderId == id);
            var results = await Task.WhenAll(
                new OrderStatusService(first, clock).ChangeAsync(id, "Shipped", 1, "synthetic-admin-A"),
                new OrderStatusService(second, clock).ChangeAsync(id, "Cancelled", 1, "synthetic-admin-B"));
            check(results.Count(result => result == StatusChangeResult.Changed) == 1 &&
                results.Count(result => result == StatusChangeResult.Conflict) == 1,
                "Concurrent status writes have one winner and one explicit conflict");
        }
        await using (var db = open())
        {
            var order = await db.Orders.SingleAsync(order => order.OrderId == id);
            var changes = await db.OrderStatusChanges.Where(change => change.OrderId == id).OrderBy(change => change.Revision).ToListAsync();
            check(order.StatusRevision == 2 && changes.Count == 2 && changes[1].PreviousStatus == "Processing" &&
                changes[1].NewStatus == order.Status && order.TotalAmount == 3 && order.ShippingCost == 1,
                "Winning history matches persisted state without touching amounts");
            db.Orders.Remove(order);
            var blocked = false;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 }) { blocked = true; }
            check(blocked, "SQL prevents deleting an order with recorded history");
        }
        await using (var db = open()) check(await db.OrderStatusChanges.CountAsync(change => change.OrderId == id) == 2 &&
            await db.Orders.AnyAsync(order => order.OrderId == id), "Rejected deletion preserves parent and complete history");
        await using (var db = open())
        {
            var downgradeBlocked = false;
            try { await db.GetService<IMigrator>().MigrateAsync("20261028150717_DurableOrderNotifications"); }
            catch (SqlException exception) when (exception.Number == 51002) { downgradeBlocked = true; }
            check(downgradeBlocked && await db.OrderStatusChanges.CountAsync(change => change.OrderId == id) == 2 &&
                !(await db.Database.GetPendingMigrationsAsync()).Any(),
                "Downgrade refuses to discard history and preserves applied migration");
        }
    }
}
