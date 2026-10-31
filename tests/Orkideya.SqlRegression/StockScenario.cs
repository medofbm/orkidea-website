using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orkideya.Application.Inventory;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

internal static class StockScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        const string actor = "synthetic-stock-admin";
        var clock = TimeProvider.System;
        async Task<(int Product, int Variant)> Setup(string name, string unit, decimal balance, decimal units)
        {
            await using var db = open();
            var product = new Product { Name = name };
            var variant = new ProductVariant { Product = product, Size = "Synthetic size", Price = 5 };
            db.ProductVariants.Add(variant); await db.SaveChangesAsync();
            var svc = new StockService(db, clock);
            check(await svc.OpenAsync(product.ProductId, unit, actor) == StockResult.Changed, "Stock setup explicitly starts at zero: " + name);
            check(await svc.ConfigureAsync(product.ProductId, variant.ProductVariantId, units, 0, actor) == StockResult.Changed, "Variant consumption configured numerically: " + name);
            check(await svc.AdjustAsync(product.ProductId, balance, 1, Guid.NewGuid(), actor, "Synthetic opening") == StockResult.Changed, "Opening quantity has a ledger movement: " + name);
            return (product.ProductId, variant.ProductVariantId);
        }
        async Task<int> Place(params (int Product, decimal Units, int Quantity, string Unit)[] lines)
        {
            await using var db = open();
            var order = new Order { CustomerName = "Synthetic inventory", WhatsAppNumber = "0000000000", City = "Test",
                ShippingAddress = "Test only", CheckoutToken = Guid.NewGuid(), CheckoutRequestHash = new string('A',64),
                OrderDate = DateTime.UtcNow, ShippingCost = 1, TotalAmount = lines.Sum(l => l.Quantity * 5m) + 1,
                Items = lines.Select(l => new OrderItem { ProductId = l.Product, UnitPrice = 5, Quantity = l.Quantity,
                    VariantSize = "Saved label", StockAccountId = l.Product, StockUnitsPerItem = l.Units, StockUnit = l.Unit }).ToList() };
            await new OrderService(db).PlaceAsync(order); return order.OrderId;
        }
        async Task<StockResult> Act(int id, int rev, string op)
        {
            await using var db = open(); return await new StockService(db, clock).ApplyOrderAsync(id, rev, op, actor);
        }
        async Task<StatusChangeResult> Cancel(int id, int rev)
        {
            await using var db = open(); return await new OrderStatusService(db, clock).ChangeAsync(id, "Cancelled", rev, actor, reason: "Synthetic cancellation");
        }
        var oil = await Setup("Synthetic oil", "ml", 1000m, 100m);
        var oilOrder = await Place((oil.Product,100,2,"ml"),(oil.Product,250,1,"ml"));
        await using (var db = open())
        {
            var row = await db.Orders.SingleAsync(o => o.OrderId == oilOrder);
            var balance = await db.StockAccounts.SingleAsync(s => s.ProductId == oil.Product);
            check(row.StockState == "AwaitingConfirmation" && balance.OnHand == 1000 && balance.Reserved == 0 &&
                !await db.StockMovements.AnyAsync(m => m.OrderId == oilOrder), "New order creates snapshots without reserving any stock");
            check(await new StockService(db,clock).ConfigureAsync(oil.Product,oil.Variant,300,2,actor) == StockResult.Changed,
                "Later size configuration can change for future orders");
        }
        check(await Act(oilOrder,0,"Reserve") == StockResult.Changed, "Manager confirmation reserves the saved oil quantity");
        await using (var db = open())
        {
            var balance = await db.StockAccounts.SingleAsync(s => s.ProductId == oil.Product);
            check(balance.OnHand == 1000 && balance.Reserved == 450 && balance.OnHand-balance.Reserved == 550,
                "Two 100ml bottles and one 250ml bottle share the same bulk account using original snapshots");
            var svc = new StockService(db,clock);
            check(await svc.ApplyOrderAsync(oilOrder,1,"Reserve",actor) == StockResult.Unchanged &&
                await db.StockMovements.CountAsync(m => m.OrderId == oilOrder && m.Kind == "Reserve") == 1,
                "Repeat confirmation cannot duplicate reservation");
            check(await svc.ApplyOrderAsync(oilOrder,0,"Consume",actor) == StockResult.Conflict, "Stale filling page is rejected");
            check(await svc.AdjustAsync(oil.Product,-600,balance.Revision,Guid.NewGuid(),actor,"Synthetic reduction") == StockResult.Insufficient,
                "Correction cannot consume reserved quantity");
            check(await new OrderStatusService(db,clock).ChangeAsync(oilOrder,"Shipped",1,actor) == StatusChangeResult.RequiresFilling,
                "Shipping cannot bypass filling for a tracked order");
            check(await new OrderStatusService(db,clock).ChangeAsync(oilOrder,"Cancelled",1,actor) == StatusChangeResult.RequiresCancellationReason,
                "Tracked cancellation requires a recorded reason");
        }
        check(await Act(oilOrder,1,"Consume") == StockResult.Changed && await Act(oilOrder,2,"Consume") == StockResult.Unchanged,
            "Filling consumes the reserved quantity exactly once");
        check(await Cancel(oilOrder,2) == StatusChangeResult.Changed, "Filled order can be cancelled with a reason");
        await using (var db = open())
        {
            var balance = await db.StockAccounts.SingleAsync(s => s.ProductId == oil.Product);
            check(balance.OnHand == 550 && balance.Reserved == 0 && !await db.StockMovements.AnyAsync(m => m.OrderId == oilOrder && m.Kind == "Release"),
                "Cancelling filled oil never adds it back to bulk stock");
            check(await db.OrderStatusChanges.AnyAsync(c => c.OrderId == oilOrder && c.Reason == "Synthetic cancellation"), "Cancellation reason is saved with order history");
        }
        check(await Act(oilOrder,3,"Reserve") == StockResult.Invalid, "Cancelled filled order cannot reserve again");
        var reservedOrder = await Place((oil.Product,100,2,"ml"));
        check(await Act(reservedOrder,0,"Reserve") == StockResult.Changed && await Cancel(reservedOrder,1) == StatusChangeResult.Changed &&
            await Cancel(reservedOrder,2) == StatusChangeResult.Unchanged, "Pre-filling cancellation releases the reservation once");
        var awaiting = await Place((oil.Product,100,1,"ml"));
        check(await Cancel(awaiting,0) == StatusChangeResult.Changed, "Unconfirmed order cancels without stock mutation");
        await using (var db = open())
        {
            var balance = await db.StockAccounts.SingleAsync(s => s.ProductId == oil.Product);
            check(balance.OnHand == 550 && balance.Reserved == 0 &&
                await db.StockMovements.CountAsync(m => m.OrderId == reservedOrder && m.Kind == "Release") == 1 &&
                !await db.StockMovements.AnyAsync(m => m.OrderId == awaiting), "Release and unconfirmed cancellation preserve actual stock");
        }
        var soap = await Setup("Synthetic soap","piece",10,1);
        await using (var db = open())
        {
            var balance = await db.StockAccounts.SingleAsync(s => s.ProductId == soap.Product);
            var svc = new StockService(db,clock);
            check(await svc.ConfigureAsync(soap.Product,soap.Variant,0.5m,balance.Revision,actor) == StockResult.Invalid &&
                await svc.AdjustAsync(soap.Product,0.5m,balance.Revision,Guid.NewGuid(),actor,"Test") == StockResult.Invalid,
                "Soap stock and variant consumption reject fractional pieces");
        }
        await using (var db = open())
        {
            var rejected = false;
            try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockAccounts SET OnHand=0.5 WHERE ProductId={soap.Product}"); }
            catch(SqlException ex) when(ex.Number==547) { rejected=true; }
            check(rejected, "SQL balance constraint also rejects fractional soap pieces");
            rejected=false;
            try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockAccounts SET Reserved=OnHand+1 WHERE ProductId={oil.Product}"); }
            catch(SqlException ex) when(ex.Number==547) { rejected=true; }
            check(rejected, "SQL balance constraint rejects reservation above actual balance");
            rejected=false;
            try { await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO OrderItems (OrderId,ProductId,Quantity,UnitPrice,StockAccountId,StockUnitsPerItem,StockUnit) VALUES ({oilOrder},{soap.Product},1,5,{oil.Product},100,N'ml')"); }
            catch(SqlException ex) when(ex.Number==547) { rejected=true; }
            check(rejected, "SQL snapshot constraint rejects an account belonging to a different product");
            var partial = new Order { CustomerName="Synthetic partial",WhatsAppNumber="0000000000",City="Test",ShippingAddress="Test only",
                CheckoutToken=Guid.NewGuid(),CheckoutRequestHash=new string('A',64),OrderDate=DateTime.UtcNow,ShippingCost=1,TotalAmount=11,
                Items=[new(){ProductId=oil.Product,Quantity=1,UnitPrice=5,StockAccountId=oil.Product,StockUnitsPerItem=100,StockUnit="ml"},
                       new(){ProductId=soap.Product,Quantity=1,UnitPrice=5}] };
            rejected=false;
            try { await new OrderService(db).PlaceAsync(partial); }
            catch(ArgumentException) { rejected=true; }
            check(rejected && !await db.Orders.AnyAsync(o=>o.CheckoutToken==partial.CheckoutToken),
                "Partially tracked aggregates cannot bypass inventory through the order write boundary");
        }
        var soapOrder = await Place((soap.Product,1,3,"piece"));
        check(await Act(soapOrder,0,"Reserve") == StockResult.Changed && await Act(soapOrder,1,"Consume") == StockResult.Changed,
            "Soap uses the same reservation lifecycle in whole pieces");
        var scarce = await Setup("Synthetic scarce oil","ml",100,100);
        var multi = await Place((oil.Product,100,1,"ml"),(scarce.Product,100,2,"ml"));
        check(await Act(multi,0,"Reserve") == StockResult.Insufficient, "Insufficient second product rejects the entire reservation");
        await using (var db = open()) check((await db.StockAccounts.SingleAsync(s=>s.ProductId==oil.Product)).Reserved == 0 &&
            !await db.StockMovements.AnyAsync(m=>m.OrderId==multi), "Rejected multi-product reservation leaves no partial quantities or movements");
        var atomic = await Place((oil.Product,100,1,"ml"),(scarce.Product,100,1,"ml"));
        check(await Act(atomic,0,"Reserve") == StockResult.Changed, "Valid multi-product order reserves atomically");
        await using (var db=open()) await db.Database.ExecuteSqlRawAsync("ALTER TABLE StockMovements ADD CONSTRAINT CK_SyntheticStockFailure CHECK (ActorUserId <> N'synthetic-failed-fill')");
        try
        {
            var failed=false;
            try { await using var failureDb=open(); await new StockService(failureDb,clock).ApplyOrderAsync(atomic,1,"Consume","synthetic-failed-fill"); }
            catch(DbUpdateException ex) when(ex.InnerException is SqlException {Number:547}) { failed=true; }
            await using var db=open();
            var a=await db.StockAccounts.SingleAsync(s=>s.ProductId==oil.Product);
            var b=await db.StockAccounts.SingleAsync(s=>s.ProductId==scarce.Product);
            check(failed && a.OnHand==550 && a.Reserved==100 && b.OnHand==100 && b.Reserved==100 &&
                (await db.Orders.SingleAsync(o=>o.OrderId==atomic)).StockState=="Reserved" && !await db.StockMovements.AnyAsync(m=>m.OrderId==atomic && m.Kind=="Consume"),
                "SQL ledger failure rolls back every account, order state and consumption movement");
        }
        finally { await using var db=open(); await db.Database.ExecuteSqlRawAsync("ALTER TABLE StockMovements DROP CONSTRAINT CK_SyntheticStockFailure"); }
        check(await Cancel(atomic,1)==StatusChangeResult.Changed,"Reserved multi-product order can release after a failed fill");
        var shared=await Setup("Synthetic shared oil","ml",1000,700);
        var one=await Place((shared.Product,700,1,"ml")); var two=await Place((shared.Product,700,1,"ml"));
        await using(var a=open()) await using(var b=open())
        {
            await a.StockAccounts.SingleAsync(s=>s.ProductId==shared.Product); await b.StockAccounts.SingleAsync(s=>s.ProductId==shared.Product);
            var results=await Task.WhenAll(new StockService(a,clock).ApplyOrderAsync(one,0,"Reserve",actor),new StockService(b,clock).ApplyOrderAsync(two,0,"Reserve",actor));
            check(results.Count(r=>r==StockResult.Changed)==1 && results.Count(r=>r==StockResult.Conflict)==1,
                "Two orders competing for one bulk balance have exactly one winner");
        }
        int winner;
        await using(var db=open())
        {
            var account=await db.StockAccounts.SingleAsync(s=>s.ProductId==shared.Product);
            winner=await db.Orders.Where(o=>(o.OrderId==one || o.OrderId==two) && o.StockState=="Reserved").Select(o=>o.OrderId).SingleAsync();
            check(account.OnHand==1000 && account.Reserved==700,"Concurrent reservations never overbook the bulk stock");
        }
        await using(var fill=open()) await using(var cancel=open())
        {
            await fill.Orders.SingleAsync(o=>o.OrderId==winner); await cancel.Orders.SingleAsync(o=>o.OrderId==winner);
            await fill.StockAccounts.SingleAsync(s=>s.ProductId==shared.Product); await cancel.StockAccounts.SingleAsync(s=>s.ProductId==shared.Product);
            var filling=new StockService(fill,clock).ApplyOrderAsync(winner,1,"Consume",actor);
            var cancelling=new OrderStatusService(cancel,clock).ChangeAsync(winner,"Cancelled",1,actor,reason:"Concurrent test cancellation");
            await Task.WhenAll(filling,cancelling);
            check(filling.Result==StockResult.Changed && cancelling.Result==StatusChangeResult.Conflict ||
                filling.Result==StockResult.Conflict && cancelling.Result==StatusChangeResult.Changed,
                "Concurrent filling and cancellation cannot both succeed");
        }
        await using(var db=open())
        {
            var account=await db.StockAccounts.SingleAsync(s=>s.ProductId==shared.Product);
            var consumed=await db.StockMovements.AnyAsync(m=>m.OrderId==winner && m.Kind=="Consume");
            var released=await db.StockMovements.AnyAsync(m=>m.OrderId==winner && m.Kind=="Release");
            check(consumed!=released && account.Reserved==0 && account.OnHand==(consumed?300:1000), "Cancellation race has one consistent ledger outcome");
            var nonce=Guid.NewGuid(); var svc=new StockService(db,clock); var revision=account.Revision;
            check(await svc.AdjustAsync(shared.Product,50,revision,nonce,actor,"Synthetic addition")==StockResult.Changed &&
                await svc.AdjustAsync(shared.Product,50,revision,nonce,actor,"Synthetic addition")==StockResult.Unchanged &&
                await svc.AdjustAsync(shared.Product,51,revision,nonce,actor,"Synthetic addition")==StockResult.Conflict,
                "Adjustment retry key prevents duplicates and rejects changed payloads");
            check(await db.StockMovements.CountAsync(m=>m.StockMovementId==nonce)==1,"A retried adjustment has one movement only");
            foreach(var id in new[]{oil.Product,soap.Product,scarce.Product,shared.Product})
            {
                var actual=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==id);
                check(actual.OnHand==await db.StockMovements.Where(m=>m.StockAccountId==id).SumAsync(m=>m.OnHandDelta) &&
                    actual.Reserved==await db.StockMovements.Where(m=>m.StockAccountId==id).SumAsync(m=>m.ReservedDelta),
                    "Ledger reconciles actual and reserved balances for account "+id);
            }
            var blocked=false;
            try { await db.GetService<IMigrator>().MigrateAsync("20261029180000_OrderStatusHistory"); }
            catch(SqlException ex) when(ex.Number==51003){blocked=true;}
            check(blocked && await db.StockMovements.AnyAsync() && !(await db.Database.GetPendingMigrationsAsync()).Any(),
                "Inventory downgrade refuses to discard stock configuration and ledger");
        }
    }
}
