using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;
using ClosedXML.Excel;

internal static class ManualOrderScenario
{
    public static async Task Run(Func<ApplicationDbContext> open,Action<bool,string> check)
    {
        const string actor="synthetic-manual-admin";
        int variantId,otherId,productId,rateId;
        await using(var db=open())
        {
            var product=new Product{Name="Synthetic manual bulk oil"};var other=new Product{Name="Synthetic manual soap"};
            var variant=new ProductVariant{Product=product,Size="100 ml",Price=12.50m,StockUnitsPerItem=100};
            var soap=new ProductVariant{Product=other,Size="piece",Price=4m};
            var rate=new ShippingRate{CityName="Synthetic manual city",Price=5m};
            db.AddRange(variant,soap,rate);await db.SaveChangesAsync();
            variantId=variant.ProductVariantId;otherId=soap.ProductVariantId;productId=product.ProductId;rateId=rate.ShippingRateId;
            db.StockAccounts.Add(new StockAccount{ProductId=productId,Unit="ml",OnHand=1000});await db.SaveChangesAsync();
            check(await db.Orders.AnyAsync(o=>o.Creation==null),"Existing orders retain unknown provenance without a synthetic backfill");
        }
        ManualOrderInput Input()=>new(){RequestId=Guid.NewGuid(),CustomerName="  Synthetic full name  ",Phone="+218000000000",Address="Synthetic address",ShippingRateId=rateId,
            Lines=[new(){VariantId=variantId,Quantity=2}]};
        async Task<ManualPreview> Preview(ManualOrderInput input){await using var db=open();return await new ManualOrderService(db,new OrderService(db)).PreviewAsync(input,actor);}
        async Task<ManualSaveResult> Save(ManualOrderDraft draft,string who=actor){await using var db=open();return await new ManualOrderService(db,new OrderService(db)).SaveAsync(draft,who);}
        var input=Input();var preview=await Preview(input);var draft=preview.Draft!;
        check(draft is not null && draft.Input.CustomerName=="Synthetic full name" && draft.Total==30 && draft.Lines.Single().StockUnitsPerItem==100,
            "Manual preview uses canonical customer, catalogue prices, carrier fee and stock consumption snapshot");
        await using(var db=open())check(!await db.Orders.AnyAsync(o=>o.CheckoutToken==draft!.RequestId) && (await db.StockAccounts.FindAsync(productId))!.Reserved==0,
            "Manual preview writes no order and reserves no stock");
        var bad=Input();bad.Phone="invalid";check((await Preview(bad)).Draft==null,"Manual preview rejects invalid phone");
        bad=Input();bad.Lines[0].Quantity=100;check((await Preview(bad)).Draft==null,"Manual preview rejects excessive quantity");
        bad=Input();bad.ShippingRateId=int.MaxValue;check((await Preview(bad)).Draft==null,"Manual preview rejects missing delivery rate");
        bad=Input();bad.Lines[0].VariantId=int.MaxValue;check((await Preview(bad)).Draft==null,"Manual preview rejects missing variant");
        bad=Input();bad.Lines.Add(new(){VariantId=otherId,Quantity=1});check((await Preview(bad)).Draft==null,"Manual preview rejects mixed tracked and untracked products");
        bad=Input();bad.Lines.Add(new(){VariantId=variantId,Quantity=98});check((await Preview(bad)).Draft==null,"Duplicate variant quantities cannot bypass the aggregate limit");
        bad=Input();bad.Lines.Add(new(){VariantId=variantId,Quantity=1});check((await Preview(bad)).Draft!.Lines.Single().Quantity==3,"Duplicate variant rows normalize to a single canonical item");
        await using(var db=open())await db.ProductVariants.Where(v=>v.ProductVariantId==variantId).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.Price,14m));
        var changed=await Save(draft!);
        check(changed.State==ManualSaveState.ReviewChanged && changed.Draft!.Total==33,"A changed price requires a fresh explicit review instead of saving stale prices");
        await using(var db=open())check(!await db.Orders.AnyAsync(o=>o.CheckoutToken==draft!.RequestId),"Price-review refresh still creates no order");
        draft=changed.Draft!;
        await using(var db=open())await db.ShippingRates.Where(r=>r.ShippingRateId==rateId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Price,6m));
        changed=await Save(draft);check(changed.State==ManualSaveState.ReviewChanged && changed.Draft!.Total==34,"Changed carrier fee requires another explicit review");draft=changed.Draft!;
        await using(var db=open())await db.ProductVariants.Where(v=>v.ProductVariantId==variantId).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.StockUnitsPerItem,120m));
        changed=await Save(draft);check(changed.State==ManualSaveState.ReviewChanged && changed.Draft!.Lines.Single().StockUnitsPerItem==120,"Changed size consumption configuration cannot be silently substituted");draft=changed.Draft!;
        check((await Save(draft,"different-admin")).State==ManualSaveState.Invalid,"Another admin cannot save a server draft owned by the first admin");
        var forged=draft with {ShippingCost=0};check((await Save(forged)).State==ManualSaveState.Invalid,"A changed server draft fails fingerprint validation");
        var saved=await Save(draft);var id=saved.OrderId!.Value;
        check(saved.State==ManualSaveState.Created,"Reviewed manual order saves successfully");
        var repeated=await Save(draft);check(repeated.State==ManualSaveState.Existing && repeated.OrderId==id,"Manual save replay returns the original order");
        await using(var db=open())
        {
            var order=await db.Orders.Include(o=>o.Creation).Include(o=>o.Items).Include(o=>o.Notifications).SingleAsync(o=>o.OrderId==id);
            check(order.Status=="Pending" && order.StatusRevision==0 && order.StockState=="AwaitingConfirmation" && order.TotalAmount==34 && order.ShippingCost==6,
                "Manual order enters the existing queue as new, awaiting confirmation, with exact totals");
            check(order.Creation is {Source:OrderSources.Facebook,ActorUserId:actor} && order.Creation.RecordedAtUtc>DateTime.UtcNow.AddMinutes(-5),
                "Manual provenance and timestamp are recorded by the server atomically");
            check(order.Items.Single().StockUnitsPerItem==120 && order.Items.Single().UnitPrice==14 && order.Items.Single().Quantity==2,
                "Manual item retains the reviewed price and consumption snapshot");
            check(order.Notifications is [{State:"Pending",Attempts:0,ProviderMessageId:null}],"Manual order creates one unsent notification intent only");
            var stock=await db.StockAccounts.FindAsync(productId);
            check(stock!.OnHand==1000 && stock.Reserved==0 && !await db.StockMovements.AnyAsync(m=>m.OrderId==id) && !await db.OrderDeliveries.AnyAsync(d=>d.OrderId==id),
                "Manual creation neither reserves, consumes, ships nor infers collection");
            check(!OrderListFilter.TryCreate("all","",out _,"untrusted-source"),"Unknown provenance filter is rejected");
            OrderListFilter.TryCreate("all","#"+id,out var filter,OrderSources.Facebook);
            check((await new OrderListQuery(db).PageAsync(filter,1)).Orders.Single().Creation?.Source==OrderSources.Facebook,"Manual source filter matches shared paginated query");
            using var wb=new XLWorkbook(new MemoryStream(new ExcelExportService().ExportOrders([order])));
            check(wb.Worksheet(1).Cell(2,12).GetString()==OrderSources.Label(OrderSources.Facebook) && wb.Worksheet(1).Cell(2,11).GetValue<decimal>()==28,
                "Excel appends source while preserving merchandise and delivery column semantics");
            var rejected=false;try{await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Orders WHERE OrderId={id}");}catch(SqlException ex)when(ex.Number==547){rejected=true;}
            check(rejected && await db.OrderCreations.AnyAsync(c=>c.OrderId==id),"SQL restricts deletion of creation provenance with its parent");
        }
        var race=(await Preview(Input())).Draft!;
        var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Save(race)));
        check(results.Count(r=>r.State==ManualSaveState.Created)==1 && results.Select(r=>r.OrderId).Distinct().Count()==1,
            "Eight concurrent manual saves create exactly one order");
        await using(var db=open())
        {
            var raceId=results[0].OrderId;
            check(await db.OrderCreations.CountAsync(c=>c.OrderId==raceId)==1 && await db.OrderNotifications.CountAsync(n=>n.OrderId==raceId)==1,
                "Concurrent manual saves retain exactly one provenance row and one notification");
        }
        var failure=(await Preview(Input())).Draft!;
        await using(var db=open())await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderCreations WITH NOCHECK ADD CONSTRAINT CK_SyntheticOriginFailure CHECK ([Source]<>'FacebookManual')");
        // The constraint must be untrusted to existing rows so it can inject only a future write failure.
        bool failed=false;try{await Save(failure);}catch(DbUpdateException){failed=true;}
        await using(var db=open())
        {
            check(failed && !await db.Orders.AnyAsync(o=>o.CheckoutToken==failure.RequestId),"Provenance failure rolls back parent, items and notification as one aggregate");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderCreations DROP CONSTRAINT CK_SyntheticOriginFailure");
            check(!await db.OrderCreations.AnyAsync(c=>!db.Orders.Any(o=>o.OrderId==c.OrderId)),"No orphan creation provenance survives failure");
            var blocked=false;try{await db.GetService<IMigrator>().MigrateAsync("20261031215916_OrderDeliveryTracking");}catch(SqlException ex)when(ex.Number==51005){blocked=true;}
            check(blocked && !(await db.Database.GetPendingMigrationsAsync()).Any(),"Downgrade refuses to erase recorded creation provenance");
        }
        check((await Save(failure)).State==ManualSaveState.Created,"A rolled-back manual save can safely recover with the same key");
    }
}
