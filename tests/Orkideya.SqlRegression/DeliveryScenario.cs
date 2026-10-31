using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orkideya.Application.Delivery;
using Orkideya.Application.Inventory;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

internal static class DeliveryScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        const string actor = "synthetic-delivery-admin";
        var clock = TimeProvider.System;
        int product;
        await using (var db = open())
        {
            var p = new Product { Name = "Synthetic delivery oil" };
            var v = new ProductVariant { Product = p, Price = 3, Size = "Synthetic100" };
            db.ProductVariants.Add(v); await db.SaveChangesAsync(); product = p.ProductId;
            var stock = new StockService(db, clock);
            await stock.OpenAsync(product, "ml", actor);
            await stock.ConfigureAsync(product, v.ProductVariantId, 100, 0, actor);
            await stock.AdjustAsync(product, 10000, 1, Guid.NewGuid(), actor, "Synthetic delivery opening");
        }
        async Task<int> Place(bool filled = true, bool tracked = true)
        {
            await using var db = open();
            var order = new Order { CustomerName = "Synthetic delivery", WhatsAppNumber = "0000000000", City = "Synthetic",
                ShippingAddress = "Synthetic only", CheckoutToken = Guid.NewGuid(), CheckoutRequestHash = new string('A',64),
                OrderDate = DateTime.UtcNow, TotalAmount = 7, ShippingCost = 1,
                Items = [new() { ProductId = product, Quantity = 2, UnitPrice = 3, VariantSize = "Saved synthetic100",
                    StockAccountId = tracked ? product : null, StockUnitsPerItem = tracked ? 100 : null, StockUnit = tracked ? "ml" : null }] };
            await new OrderService(db).PlaceAsync(order);
            if (tracked && filled)
            {
                var stock = new StockService(db, clock);
                await stock.ApplyOrderAsync(order.OrderId, 0, "Reserve", actor);
                await stock.ApplyOrderAsync(order.OrderId, 1, "Consume", actor);
            }
            if (!tracked) await new OrderStatusService(db, clock).ChangeAsync(order.OrderId, "Processing", 0, actor);
            return order.OrderId;
        }
        DeliveryInput Input(string action, int revision) => new() { Action = action, ExpectedRevision = revision, RequestId = Guid.NewGuid() };
        DeliveryInput Handoff(int revision) => new() { Action = "HandOver", ExpectedRevision = revision,
            RequestId = Guid.NewGuid(), Mode = "Company", CarrierName = "Synthetic courier", ShipmentReference = "SYNTHETIC-SHIP" };
        async Task<DeliveryResult> Act(int id, DeliveryInput request, string user = actor)
        {
            await using var db = open(); return await new DeliveryService(db, clock).ApplyAsync(id, request, user);
        }
        async Task RejectSql(FormattableString sql, string label)
        {
            await using var db = open(); var rejected = false;
            try { await db.Database.ExecuteSqlInterpolatedAsync(sql); }
            catch (SqlException ex) when (ex.Number == 547) { rejected = true; }
            check(rejected, label);
        }
        var pending = await Place(filled: false);
        check(await Act(pending, Handoff(0)) == DeliveryResult.RequiresPreparation,
            "Tracked delivery cannot bypass confirmation and filling");
        var id = await Place();
        decimal balance;
        await using (var db = open()) balance = (await db.StockAccounts.AsNoTracking().SingleAsync(s => s.ProductId == product)).OnHand;
        await using (var db = open()) check(await new OrderStatusService(db,clock).ChangeAsync(id,"Shipped",2,actor)==StatusChangeResult.RequiresDeliveryOperation,
            "Filled tracked order cannot bypass explicit handoff through generic shipping status");
        var handoff = Handoff(2);
        check(await Act(id, handoff) == DeliveryResult.Changed, "Filled order hands over to the courier explicitly");
        check(await Act(id, handoff) == DeliveryResult.Unchanged, "Identical delivery request replay is an audited no-op");
        handoff.ShipmentReference = "CHANGED-PAYLOAD";
        check(await Act(id, handoff) == DeliveryResult.Conflict, "Delivery replay key cannot be reused with changed payload");
        handoff.ShipmentReference = "SYNTHETIC-SHIP";
        check(await Act(id, handoff, "different-admin") == DeliveryResult.Conflict, "Replay is bound to authenticated actor");
        var another = await Place();
        check(await Act(another, handoff) == DeliveryResult.Conflict, "Replay key cannot apply to another order");
        await using (var db = open())
        {
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderId == id);
            var shipment = await db.OrderDeliveries.AsNoTracking().SingleAsync(d => d.OrderId == id);
            check(order.Status == "Shipped" && order.StatusRevision == 3 && shipment.State == "HandedOver" && shipment.Revision == 3
                && await db.OrderDeliveryEvents.CountAsync(e => e.OrderId == id) == 1,
                "Handoff commits one shipment/event and matching order status revision atomically");
            check(await new OrderStatusService(db, clock).ChangeAsync(id, "Delivered", 3, actor) == StatusChangeResult.RequiresDeliveryOperation,
                "Generic order status cannot bypass delivery history");
        }
        var noReason = Input("MarkReturning",3);
        check(await Act(id,noReason) == DeliveryResult.Invalid, "Non-delivery requires an explicit reason");
        var returning = Input("MarkReturning",3); returning.Reason = "Synthetic no answer";
        check(await Act(id, returning) == DeliveryResult.Changed, "Unreachable customer records awaiting return without claiming physical receipt");
        var receive = Input("ReceiveReturn",4);
        check(await Act(id, receive) == DeliveryResult.Invalid, "Return receipt requires explicit confirmation of receiving the whole package");
        receive.ConfirmReturnReceived = true;
        check(await Act(id, receive) == DeliveryResult.Changed, "Actual return receipt is a separate committed operation");
        check(await Act(id, receive) == DeliveryResult.Unchanged, "Return receipt replay records no second receipt");
        await using (var db = open())
        {
            var row = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderId == id);
            check(row.Status == "Cancelled" && row.StatusRevision == 5 && row.StockState == "Filled"
                && await db.OrderDeliveryEvents.AnyAsync(e => e.OrderId == id && e.ReturnReceiptConfirmed),
                "Received non-delivery return closes the order with filled stock unchanged and explicit receipt evidence");
        }
        var redispatch = new DeliveryInput { Action = "Redispatch", ExpectedRevision = 5, RequestId = Guid.NewGuid(),
            Mode = "Self", CarrierName = "discard for self", Reason = "Synthetic same package reviewed" };
        check(await Act(id,redispatch) == DeliveryResult.Invalid, "Returned package cannot redispatch without reviewing safety and same-package reuse");
        redispatch.ConfirmSamePackageReusable = true;
        check(await Act(id,redispatch) == DeliveryResult.Changed, "Reviewed same returned package can redispatch for its original order without new filling");
        var deliver = Input("Deliver",6);
        check(await Act(id,deliver) == DeliveryResult.Changed, "Customer receipt follows handover and closes delivery");
        var afterDelivered = Input("MarkReturning",7); afterDelivered.Reason = "Synthetic actual delivered return rejected";
        check(await Act(id,afterDelivered) == DeliveryResult.Invalid, "Delivered order is not a normal non-delivery return");
        var corrected = Input("CorrectState",7); corrected.TargetState = "HandedOver";
        check(await Act(id,corrected) == DeliveryResult.Invalid, "Mistaken delivered-state correction requires a reason");
        corrected.Reason = "Synthetic incorrect receipt entry";
        check(await Act(id,corrected) == DeliveryResult.Changed, "Wrong receipt can be corrected without deleting the previous event");
        var resumeFail = Input("MarkReturning",8); resumeFail.Reason = "Synthetic no answer again";
        await Act(id,resumeFail);
        var resume = Input("Resume",9); resume.Reason = "Synthetic customer answered before return";
        check(await Act(id,resume) == DeliveryResult.Changed, "Delivery may resume before physical return without another stock movement");
        var details = new DeliveryInput { Action = "CorrectDetails", ExpectedRevision = 10, RequestId = Guid.NewGuid(), Mode = "Company",
            CarrierName = "Synthetic corrected carrier", ShipmentReference = "CORRECTED-SYNTHETIC", Reason = "Synthetic metadata correction" };
        check(await Act(id,details) == DeliveryResult.Changed, "Carrier/reference correction stores a new event and reason");
        await using (var db = open())
        {
            var ledger = await db.OrderDeliveryEvents.AsNoTracking().Where(e => e.OrderId == id).OrderBy(e => e.Revision).ToListAsync();
            check(ledger.First().CarrierName == "Synthetic courier" && ledger.Any(e => e.SamePackageReuseConfirmed && e.Mode == "Self" && e.CarrierName == null)
                && ledger.Last().CarrierName == "Synthetic corrected carrier" && ledger.Count == 9,
                "Immutable delivery snapshots preserve original carrier and self/redispatch/correction history");
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderId == id);
            check(order.TotalAmount == 7 && order.ShippingCost == 1 && order.StockState == "Filled",
                "Delivery actions never edit saved merchandise/delivery amounts or filled-stock state");
            check(await db.StockMovements.CountAsync(m => m.OrderId == id && m.Kind == "Consume") == 1
                && await db.StockMovements.CountAsync(m => m.OrderId == id && m.Kind == "Reserve") == 1,
                "Return, receipt and redispatch do not consume or reserve bulk stock again");
        }
        // Only the separately filled 'another' fixture changed physical balance after capture.
        await using (var db = open()) check((await db.StockAccounts.AsNoTracking().SingleAsync(s => s.ProductId == product)).OnHand == balance - 200,
            "All delivery lifecycle actions leave physical/reserved inventory untouched");
        await RejectSql($"UPDATE OrderDeliveries SET State=N'Teleported' WHERE OrderId={id}", "SQL rejects unknown current delivery states");
        await RejectSql($"UPDATE OrderDeliveries SET CarrierName=N' ' WHERE OrderId={id}", "SQL requires company name for company deliveries");
        await RejectSql($"UPDATE OrderDeliveries SET Mode=N'Self' WHERE OrderId={id}", "SQL prevents self-delivery from retaining a company name");
        await RejectSql($"UPDATE OrderDeliveryEvents SET NewState=N'Returned' WHERE OrderId={id} AND Action=N'HandOver'", "SQL rejects impossible historical transitions");
        await RejectSql($"UPDATE OrderDeliveryEvents SET ReturnReceiptConfirmed=0 WHERE OrderId={id} AND Action=N'ReceiveReturn'", "SQL requires return receipt evidence");
        await RejectSql($"UPDATE OrderDeliveryEvents SET Reason=NULL WHERE OrderId={id} AND Action=N'MarkReturning'", "SQL preserves mandatory failure/correction reasons");
        await RejectSql($"DELETE FROM OrderDeliveries WHERE OrderId={id}", "SQL prevents deleting a shipment with audit history");
        await RejectSql($"DELETE FROM Orders WHERE OrderId={id}", "SQL prevents deleting an order with delivery history");

        var legacy = await Place(tracked:false);
        var oldHandoff = Handoff(1);
        check(await Act(legacy,oldHandoff) == DeliveryResult.RequiresPreparation, "Legacy untracked shipment requires explicit manual preparation confirmation");
        oldHandoff.ConfirmLegacyPrepared = true;
        check(await Act(legacy,oldHandoff) == DeliveryResult.Changed, "Confirmed legacy preparation is recorded without inventing stock snapshots");
        var emptyOrder = new Order { CustomerName="Synthetic empty delivery", WhatsAppNumber="0000000000", City="Test", ShippingAddress="Test", Status="Processing", OrderDate=DateTime.UtcNow };
        await using(var db=open()){ db.Orders.Add(emptyOrder); await db.SaveChangesAsync(); }
        var emptyHandoff=Handoff(0);emptyHandoff.ConfirmLegacyPrepared=true;
        check(await Act(emptyOrder.OrderId,emptyHandoff)==DeliveryResult.RequiresPreparation,"Empty historical order cannot hand over a nonexistent package");

        var same = await Place(); var sameRequest = Handoff(2);
        var repeats = await Task.WhenAll(Enumerable.Range(0,8).Select(_ => Act(same,sameRequest)));
        check(repeats.Count(r=>r==DeliveryResult.Changed)==1 && repeats.Count(r=>r==DeliveryResult.Unchanged)==7,
            "Eight identical concurrent delivery requests commit once and resolve as idempotent replays");
        var different = await Place();
        var competing = await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Act(different,Handoff(2))));
        check(competing.Count(r=>r==DeliveryResult.Changed)==1 && competing.Count(r=>r==DeliveryResult.Conflict)==7,
            "Eight different handoff requests at one order version commit only one shipment");
        var fail = Input("MarkReturning",3);fail.Reason="Synthetic race failure";
        var terminal = await Task.WhenAll(Act(different,Input("Deliver",3)),Act(different,fail));
        check(terminal.Count(r=>r==DeliveryResult.Changed)==1 && terminal.Count(r=>r==DeliveryResult.Conflict)==1,
            "Delivery-versus-return race commits exactly one coherent outcome");
        await using(var db=open())
        {
            var current=await db.OrderDeliveries.AsNoTracking().SingleAsync(d=>d.OrderId==different);
            var order=await db.Orders.AsNoTracking().SingleAsync(o=>o.OrderId==different);
            check(order.StatusRevision==4 && current.Revision==4 &&
                order.Status==(current.State=="Delivered"?"Delivered":"Shipped") && await db.OrderDeliveryEvents.CountAsync(e=>e.OrderId==different)==2,
                "Concurrent terminal action preserves matching order/delivery revisions and one new event");
        }
        var rollback = await Place(); var rollbackRequest = Handoff(2); rollbackRequest.Reason="FAIL_DELIVERY_EVENT";
        await using(var db=open()) await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderDeliveryEvents ADD CONSTRAINT CK_SyntheticDeliveryFailure CHECK (Reason IS NULL OR Reason<>N'FAIL_DELIVERY_EVENT')");
        var rejected=false;
        try{await Act(rollback,rollbackRequest);}catch(DbUpdateException ex)when(ex.InnerException is SqlException{Number:547}){rejected=true;}
        await using(var db=open())
        {
            var order=await db.Orders.AsNoTracking().SingleAsync(o=>o.OrderId==rollback);
            check(rejected && order.Status=="Processing" && order.StatusRevision==2 &&
                !await db.OrderDeliveries.AnyAsync(d=>d.OrderId==rollback) && !await db.OrderDeliveryEvents.AnyAsync(e=>e.OrderId==rollback)
                && await db.OrderStatusChanges.CountAsync(e=>e.OrderId==rollback)==1,
                "Injected event SQL failure rolls back shipment, order status/revision and matching status history together");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderDeliveryEvents DROP CONSTRAINT CK_SyntheticDeliveryFailure");
        }
        check(await Act(rollback,rollbackRequest)==DeliveryResult.Changed,"Failed atomic handoff can retry the same operation key after recovery");
        await using(var db=open())
        {
            var account=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==product);
            var sums=await db.StockMovements.Where(m=>m.StockAccountId==product).GroupBy(m=>m.StockAccountId)
                .Select(g=>new{Physical=g.Sum(m=>m.OnHandDelta),Reserved=g.Sum(m=>m.ReservedDelta)}).SingleAsync();
            check(account.OnHand==sums.Physical && account.Reserved==sums.Reserved,"Delivery suite preserves stock ledger reconciliation");
            check(!await db.OrderNotifications.AnyAsync(n=>db.OrderDeliveries.Any(d=>d.OrderId==n.OrderId) && (n.Attempts!=0 || n.ProviderMessageId!=null)),
                "Delivery operations never attempt a provider notification");
            var blocked=false;
            try{await db.GetService<IMigrator>().MigrateAsync("20261031155358_BulkStockReservations");}catch(SqlException ex)when(ex.Number==51004){blocked=true;}
            check(blocked && await db.OrderDeliveryEvents.AnyAsync() && !(await db.Database.GetPendingMigrationsAsync()).Any(),
                "Delivery downgrade refuses to erase shipment state or immutable event history");
        }
    }
}
