using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

internal static class ShippingQuoteScenario
{
    public static async Task Run(Func<ApplicationDbContext> open,Action<bool,string> check)
    {
        Order Make(Guid token)
        {
            var now=DateTimeOffset.UtcNow;
            return new(){CheckoutToken=token,CheckoutRequestHash=new string('D',64),CustomerName="Synthetic quote QA",WhatsAppNumber="+218000000000",ShippingAddress="Synthetic address",City="Synthetic city",ShippingCost=15,TotalAmount=40,OrderDate=DateTime.UtcNow,
                Items=[new(){ProductId=1,Quantity=1,UnitPrice=25,VariantSize="Synthetic"}],
                ShippingQuote=new(){Provider="Sabil",ServiceId=new string('a',24),BranchId="synthetic-branch",City="Synthetic city",Area="Synthetic area",OriginRevision="v1",HandoffMode="OfficeDropoff",Currency="lyd",ShippingFee=15,QuotedAtUtc=now,ExpiresAtUtc=now.AddMinutes(5)}};
        }
        await using(var db=open())check(!await db.OrderShippingQuotes.AnyAsync(),"Snapshot migration creates no fabricated carrier provenance for existing orders");
        var token=Guid.NewGuid();int id;
        await using(var db=open())
        {
            var placed=await new OrderService(db).PlaceAsync(Make(token));id=placed.Order.OrderId;
            check(placed.IsNew && placed.Order.ShippingQuote!.OrderId==id,"Carrier snapshot is saved in the new order aggregate");
        }
        await using(var db=open())
        {
            var saved=await db.Orders.Include(o=>o.ShippingQuote).SingleAsync(o=>o.OrderId==id);
            check(saved.ShippingQuote is {ShippingFee:15,Currency:"lyd",Area:"Synthetic area"} && saved.ShippingQuote.AcceptedAtUtc>=saved.ShippingQuote.QuotedAtUtc,
                "Persisted quote facts and server acceptance time match the accepted order");
            check(await db.OrderNotifications.CountAsync(n=>n.OrderId==id)==1 && await db.OrderItems.CountAsync(i=>i.OrderId==id)==1,
                "Snapshot order retains exactly one item and notification intent");
            var retry=Make(token);retry.ShippingQuote!.QuotedAtUtc=DateTimeOffset.UtcNow.AddHours(-1);retry.ShippingQuote.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-30);
            var same=await new OrderService(db).PlaceAsync(retry);
            check(!same.IsNew && same.Order.OrderId==id && await db.OrderShippingQuotes.CountAsync(q=>q.OrderId==id)==1,
                "Completed order retry returns original snapshot even after new quote context expires");
        }
        foreach(var mutate in new Action<Order>[] {o=>o.ShippingQuote!.ShippingFee=10,o=>o.ShippingQuote!.City="Other",o=>o.ShippingQuote!.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1),o=>o.ShippingQuote!.Currency="usd"})
        {
            await using var db=open();var invalid=Make(Guid.NewGuid());mutate(invalid);var rejected=false;
            try{await new OrderService(db).PlaceAsync(invalid);}catch(ArgumentException){rejected=true;}
            check(rejected && !await db.Orders.AnyAsync(o=>o.CheckoutToken==invalid.CheckoutToken),"Invalid shipping snapshot is rejected without persisting an order");
        }
        await using(var db=open())await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderShippingQuotes ADD CONSTRAINT CK_SyntheticSnapshotFailure CHECK (Area <> N'FAIL_QA')");
        var failed=Make(Guid.NewGuid());failed.ShippingQuote!.Area="FAIL_QA";
        await using(var db=open())
        {
            var rejected=false;try{await new OrderService(db).PlaceAsync(failed);}catch(DbUpdateException e)when(e.InnerException is SqlException{Number:547}){rejected=true;}
            check(rejected,"SQL snapshot insert failure reaches the caller");
        }
        await using(var db=open())
        {
            check(!await db.Orders.AnyAsync(o=>o.CheckoutToken==failed.CheckoutToken),"Snapshot insert failure rolls back parent order atomically");
            check(!await db.OrderShippingQuotes.AnyAsync(q=>!db.Orders.Any(o=>o.OrderId==q.OrderId)) && !await db.OrderNotifications.AnyAsync(n=>!db.Orders.Any(o=>o.OrderId==n.OrderId)),
                "Failed shipping snapshot leaves no orphan quote or notification");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderShippingQuotes DROP CONSTRAINT CK_SyntheticSnapshotFailure");
        }
        var parallelToken=Guid.NewGuid();
        var parallel=await Task.WhenAll(Enumerable.Range(0,4).Select(async _=>{await using var db=open();return await new OrderService(db).PlaceAsync(Make(parallelToken));}));
        check(parallel.Count(p=>p.IsNew)==1 && parallel.Select(p=>p.Order.OrderId).Distinct().Count()==1,"Concurrent carrier checkout retries preserve one order identity");
        await using(var db=open())
        {
            var orderId=parallel[0].Order.OrderId;
            check(await db.OrderShippingQuotes.CountAsync(q=>q.OrderId==orderId)==1 && await db.OrderNotifications.CountAsync(n=>n.OrderId==orderId)==1,
                "Concurrent carrier checkout saves one snapshot and notification");
            var guarded=false;try{await db.GetService<IMigrator>().MigrateAsync("20261031235949_ManualOrderCreation");}catch(SqlException e)when(e.Number==51006){guarded=true;}
            check(guarded,"Migration rollback refuses to erase recorded shipping quote provenance");
            await db.Database.MigrateAsync();
        }
    }
}
