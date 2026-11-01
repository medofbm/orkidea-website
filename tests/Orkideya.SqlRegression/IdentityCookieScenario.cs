using Orkideya.Application.Orders;
using Orkideya.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orkideya.Controllers;
using Orkideya.Data;
using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

internal static class IdentityCookieScenario
{
    public static async Task Run(string connection, Action<bool, string> check)
    {
        using var factory = new WebApplicationFactory<CartController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection,
                    ["Initialization:Run"] = "false",
                    ["Notifications:WhatsAppEnabled"] = "false",
                    ["Notifications:OutboxWorkerEnabled"] = "false",
                    ["AllowedHosts"] = "localhost",
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var guest = Client();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var password = "Synthetic_Aa1_" + Guid.NewGuid().ToString("N");
        async Task<IdentityUser> Seed(string email, bool admin)
        {
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            check((await users.CreateAsync(user, password)).Succeeded, "Real Identity SQL test account created: " + (admin ? "admin" : "customer"));
            if (admin)
            {
                if (!await roles.RoleExistsAsync("Admin"))
                    check((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded, "Real Identity admin test role created");
                check((await users.AddToRoleAsync(user, "Admin")).Succeeded, "Real Identity SQL admin role persisted");
            }
            return user;
        }
        var customer = await Seed("cookie-customer@orkidea.test", false);
        var adminUser = await Seed("cookie-admin@orkidea.test", true);
        check(!(await users.GetRolesAsync(customer)).Contains("Admin"), "Real customer account has no Admin role");

        async Task<string> Token(HttpClient client, string path)
        {
            using var page = await client.GetAsync(path);
            check(page.StatusCode == HttpStatusCode.OK, "Real cookie flow form renders: " + path);
            var html = await page.Content.ReadAsStringAsync();
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!match.Success) throw new InvalidOperationException("Missing real antiforgery field");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task Login(HttpClient client, string email)
        {
            var token = await Token(client, "/Identity/Account/Login");
            using var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email, ["Input.Password"] = password,
                ["Input.RememberMe"] = "false", ["__RequestVerificationToken"] = token
            }));
            check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
                cookies.Any(c => c.Contains("Identity.Application=", StringComparison.Ordinal)), "Real login issues Identity application cookie");
        }
        bool LoginRequired(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Redirect &&
            response.Headers.Location?.OriginalString.Contains("/Identity/Account/Login", StringComparison.Ordinal) == true;
        bool Denied(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Forbidden ||
            (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString.Contains("/Identity/Account/AccessDenied", StringComparison.Ordinal) == true);
        foreach (var path in new[] { "/Admin", "/Admin/Orders", "/Admin/Orders/ExportToExcel", "/Admin/Inventory", "/Admin/Inventory/Details/1", "/Admin/Delivery", "/Admin/ManualOrders/Create" })
        {
            using var response = await guest.GetAsync(path);
            check(LoginRequired(response), "Anonymous real-cookie host cannot read admin: " + path);
        }
        using var buyer = Client();
        await Login(buyer, customer.Email!);
        using (var manage = await buyer.GetAsync("/Identity/Account/Manage"))
            check(manage.StatusCode == HttpStatusCode.OK, "Customer cookie accesses own account");
        foreach (var path in new[] { "/Admin", "/Admin/Products", "/Admin/Orders/Details/1", "/Admin/Orders/ExportToExcel", "/Admin/Inventory", "/Admin/Inventory/Details/1", "/Admin/Delivery", "/Admin/ManualOrders/Create" })
        {
            using var response = await buyer.GetAsync(path);
            check(Denied(response), "Real customer cookie denied admin access: " + path);
        }
        using (var response = await buyer.PostAsync("/Admin/Orders/UpdateStatus?id=1&status=delivered", new FormUrlEncodedContent(new Dictionary<string, string>())))
            check(Denied(response), "Real customer cookie cannot update admin order");
        using var adminClient = Client();
        await Login(adminClient, "cookie-admin@orkidea.test");
        using (var dashboard = await adminClient.GetAsync("/Admin"))
            check(dashboard.StatusCode == HttpStatusCode.OK, "Real admin cookie renders database-backed dashboard");
        using (var export = await adminClient.GetAsync("/Admin/Orders/ExportToExcel"))
        {
            check(export.StatusCode == HttpStatusCode.OK && export.Content.Headers.ContentType?.MediaType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Real admin cookie downloads private XLSX over HTTP");
            using var stream = new MemoryStream(await export.Content.ReadAsByteArrayAsync());
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet(1);
            check(sheet.RightToLeft && sheet.LastRowUsed()!.RowNumber() == await db.Orders.CountAsync() + 1,
                "Authenticated HTTP export contains all SQL rows and RTL sheet");
            var total = sheet.RowsUsed().Skip(1).Sum(row => row.Cell(7).GetValue<decimal>());
            check(total == await db.Orders.SumAsync(o => o.TotalAmount), "Authenticated XLSX amounts match SQL without adding shipping twice");
            var merchandise = sheet.RowsUsed().Skip(1).Sum(row => row.Cell(11).GetValue<decimal>());
            check(merchandise == await db.Orders.SumAsync(o => o.TotalAmount - o.ShippingCost),
                "Authenticated XLSX merchandise values match saved SQL amounts excluding delivery");
        }
        using (var invalidList = await adminClient.GetAsync("/Admin/Orders?page=not-a-number"))
            check(invalidList.StatusCode == HttpStatusCode.BadRequest, "Real admin route rejects malformed page binding");
        using (var invalidExport = await adminClient.GetAsync("/Admin/Orders/ExportToExcel?status=unknown"))
            check(invalidExport.StatusCode == HttpStatusCode.BadRequest, "Real admin export rejects unsupported status");
        using (var pagedList = await adminClient.GetAsync("/Admin/Orders?page=2147483647&search=ListQA_"))
            check(pagedList.StatusCode == HttpStatusCode.OK && (await pagedList.Content.ReadAsStringAsync()).Contains("rel=\"prev\""),
                "Real Razor page clamps oversized page and renders navigation for a multi-page search");
        var audited = await db.Orders.AsNoTracking().FirstAsync(order => order.Status == "Pending" && order.StockState == "Untracked");
        var statusToken = await Token(adminClient, "/Admin/Orders");
        Dictionary<string, string> StatusForm(int revision) => new()
        {
            ["__RequestVerificationToken"] = statusToken, ["id"] = audited.OrderId.ToString(),
            ["status"] = "processing", ["expectedRevision"] = revision.ToString(), ["actorUserId"] = "forged-client-actor"
        };
        using (var changed = await adminClient.PostAsync("/Admin/Orders/UpdateStatus", new FormUrlEncodedContent(StatusForm(audited.StatusRevision))))
            check(changed.StatusCode == HttpStatusCode.OK, "Real admin cookie and antiforgery change order status");
        var savedHistory = await db.OrderStatusChanges.AsNoTracking().SingleAsync(change => change.OrderId == audited.OrderId);
        check(savedHistory.ActorUserId == adminUser.Id && savedHistory.NewStatus == "Processing",
            "Audit actor comes from authenticated identity, never client input");
        using (var stale = await adminClient.PostAsync("/Admin/Orders/UpdateStatus", new FormUrlEncodedContent(StatusForm(audited.StatusRevision))))
            check(stale.StatusCode == HttpStatusCode.Conflict, "Stale authenticated page gets HTTP409 instead of overwriting status");
        using (var details = await adminClient.GetAsync($"/Admin/Orders/Details/{audited.OrderId}"))
            check(details.StatusCode == HttpStatusCode.OK && (await details.Content.ReadAsStringAsync()).Contains("status-history-title"),
                "Authenticated details render saved status history");
        using (var deleted = await adminClient.PostAsync($"/Admin/Orders/Delete/{audited.OrderId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = statusToken })))
            check(deleted.StatusCode == HttpStatusCode.Redirect && await db.Orders.AsNoTracking().AnyAsync(order => order.OrderId == audited.OrderId),
                "Admin delete preserves an order with recorded history");
        var httpProduct = new Product { Name = "Synthetic HTTP stock" };
        var httpVariant = new ProductVariant { Product = httpProduct, Size = "50ml test", Price = 2 };
        db.ProductVariants.Add(httpVariant); await db.SaveChangesAsync();
        var inventoryToken = await Token(adminClient, "/Admin/Inventory");
        async Task PostStock(string action, Dictionary<string,string> fields)
        {
            fields["__RequestVerificationToken"] = inventoryToken;
            using var response = await adminClient.PostAsync($"/Admin/Inventory/{action}/{httpProduct.ProductId}",new FormUrlEncodedContent(fields));
            check(response.StatusCode==HttpStatusCode.Redirect,"Real admin inventory form succeeds: "+action);
        }
        await PostStock("Open",new(){{"unit","ml"}});
        await PostStock("Configure",new(){{"VariantId",httpVariant.ProductVariantId.ToString()},{"Units","50"},{"ExpectedRevision","0"}});
        var receiptKey=Guid.NewGuid().ToString();
        Dictionary<string,string> Receipt() => new(){{"Delta","500"},{"ExpectedRevision","1"},{"RequestId",receiptKey},{"Reason","Synthetic HTTP opening"},{"ActorUserId","forged-actor"}};
        await PostStock("Adjust",Receipt()); await PostStock("Adjust",Receipt());
        var httpStock=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==httpProduct.ProductId);
        check(httpStock.OnHand==500 && httpStock.Reserved==0 &&
            await db.StockMovements.CountAsync(m=>m.StockAccountId==httpProduct.ProductId && m.Kind=="Adjust")==1 &&
            await db.StockMovements.AnyAsync(m=>m.StockAccountId==httpProduct.ProductId && m.ActorUserId==adminUser.Id && m.Kind=="Adjust"),
            "Real HTTP receipt retry saves once with server-side identity");
        using(var stockDetails=await adminClient.GetAsync($"/Admin/Inventory/Details/{httpProduct.ProductId}"))
            check(stockDetails.StatusCode==HttpStatusCode.OK,"Real admin stock details render quantities, size form and ledger");
        var httpOrder=new Order { CustomerName="Synthetic HTTP inventory",WhatsAppNumber="0000000000",City="Test",ShippingAddress="Test only",
            CheckoutToken=Guid.NewGuid(),CheckoutRequestHash=new string('A',64),OrderDate=DateTime.UtcNow,ShippingCost=1,TotalAmount=5,
            Items=[new(){ProductId=httpProduct.ProductId,Quantity=2,UnitPrice=2,VariantSize="50ml test",StockAccountId=httpProduct.ProductId,StockUnitsPerItem=50,StockUnit="ml"}] };
        await new OrderService(db).PlaceAsync(httpOrder);
        var orderToken=await Token(adminClient,$"/Admin/Orders/Details/{httpOrder.OrderId}");
        async Task Operate(string operation,int revision)
        {
            using var response=await adminClient.PostAsync($"/Admin/Inventory/OrderOperation/{httpOrder.OrderId}",new FormUrlEncodedContent(new Dictionary<string,string>
                {{"__RequestVerificationToken",orderToken},{"operation",operation},{"expectedRevision",revision.ToString()}}));
            check(response.StatusCode==HttpStatusCode.Redirect,"Real admin stock order operation completes: "+operation);
        }
        await Operate("Reserve",0); await Operate("Consume",0);
        httpStock=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==httpProduct.ProductId);
        check(httpStock.OnHand==500 && httpStock.Reserved==100,"Real stale filling form cannot overwrite a confirmed reservation");
        await Operate("Consume",1);
        using(var cancelled=await adminClient.PostAsync($"/Admin/Inventory/Cancel/{httpOrder.OrderId}",new FormUrlEncodedContent(new Dictionary<string,string>
            {{"__RequestVerificationToken",orderToken},{"expectedRevision","2"},{"reason","Synthetic HTTP cancellation"}})))
            check(cancelled.StatusCode==HttpStatusCode.Redirect,"Real admin cancellation form preserves a reason");
        httpStock=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==httpProduct.ProductId);
        var httpSaved=await db.Orders.AsNoTracking().SingleAsync(o=>o.OrderId==httpOrder.OrderId);
        check(httpSaved.Status=="Cancelled" && httpSaved.StockState=="Filled" && httpStock.OnHand==400 && httpStock.Reserved==0,
            "Real admin reserve-fill-cancel flow does not return consumed oil");
        var deliveryOrder = new Order { CustomerName="Synthetic HTTP delivery", WhatsAppNumber="0000000000", City="Test", ShippingAddress="Test only",
            CheckoutToken=Guid.NewGuid(),CheckoutRequestHash=new string('A',64),OrderDate=DateTime.UtcNow,ShippingCost=1,TotalAmount=3,
            Items=[new(){ProductId=httpProduct.ProductId,Quantity=1,UnitPrice=2,VariantSize="50ml test",StockAccountId=httpProduct.ProductId,StockUnitsPerItem=50,StockUnit="ml"}] };
        await new OrderService(db).PlaceAsync(deliveryOrder);
        var stockForDelivery = new Orkideya.Application.Inventory.StockService(db,TimeProvider.System);
        await stockForDelivery.ApplyOrderAsync(deliveryOrder.OrderId,0,"Reserve",adminUser.Id);
        await stockForDelivery.ApplyOrderAsync(deliveryOrder.OrderId,1,"Consume",adminUser.Id);
        var deliveryToken=await Token(adminClient,$"/Admin/Orders/Details/{deliveryOrder.OrderId}");
        Dictionary<string,string> DeliveryForm(string action,int revision) => new()
        {
            {"__RequestVerificationToken",deliveryToken},{"Action",action},{"ExpectedRevision",revision.ToString()},
            {"RequestId",Guid.NewGuid().ToString()},{"ActorUserId","forged-delivery-actor"}
        };
        async Task PostDelivery(Dictionary<string,string> form)
        {
            using var response=await adminClient.PostAsync($"/Admin/Delivery/Apply/{deliveryOrder.OrderId}",new FormUrlEncodedContent(form));
            check(response.StatusCode==HttpStatusCode.Redirect,"Authenticated delivery form returns to order details: "+form["Action"]);
        }
        using(var noCsrf=await adminClient.PostAsync($"/Admin/Delivery/Apply/{deliveryOrder.OrderId}",new FormUrlEncodedContent(new Dictionary<string,string>{{"Action","HandOver"}})))
            check(noCsrf.StatusCode==HttpStatusCode.BadRequest,"Real admin delivery mutation requires antiforgery");
        var httpHandoff=DeliveryForm("HandOver",2);httpHandoff["Mode"]="Company";httpHandoff["CarrierName"]="Synthetic HTTP carrier";httpHandoff["ShipmentReference"]="HTTP-QA-SHIP";
        await PostDelivery(httpHandoff);await PostDelivery(httpHandoff);
        check(await db.OrderDeliveryEvents.CountAsync(e=>e.OrderId==deliveryOrder.OrderId)==1 &&
            await db.OrderDeliveryEvents.AnyAsync(e=>e.OrderId==deliveryOrder.OrderId && e.ActorUserId==adminUser.Id),
            "Real HTTP handoff replay stores one event with authenticated actor rather than posted actor");
        var staleDelivery=DeliveryForm("MarkReturning",2);staleDelivery["Reason"]="Synthetic stale page";await PostDelivery(staleDelivery);
        check(await db.OrderDeliveryEvents.CountAsync(e=>e.OrderId==deliveryOrder.OrderId)==1,
            "Stale real delivery form cannot change a newer shipment");
        using(var bypass=await adminClient.PostAsync("/Admin/Orders/UpdateStatus",new FormUrlEncodedContent(new Dictionary<string,string>
            {{"__RequestVerificationToken",deliveryToken},{"id",deliveryOrder.OrderId.ToString()},{"status","delivered"},{"expectedRevision","3"}})))
            check(bypass.StatusCode==HttpStatusCode.BadRequest,"Real generic status route cannot bypass registered delivery workflow");
        var failedDelivery=DeliveryForm("MarkReturning",3);failedDelivery["Reason"]="Synthetic client unreachable";await PostDelivery(failedDelivery);
        var returnedDelivery=DeliveryForm("ReceiveReturn",4);await PostDelivery(returnedDelivery);
        check(await db.OrderDeliveries.AsNoTracking().AnyAsync(d=>d.OrderId==deliveryOrder.OrderId && d.State=="Returning"),
            "Real return form without physical receipt confirmation remains awaiting return");
        returnedDelivery["ConfirmReturnReceived"]="true";await PostDelivery(returnedDelivery);
        var resend=DeliveryForm("Redispatch",5);resend["Mode"]="Self";resend["ConfirmSamePackageReusable"]="true";resend["Reason"]="Synthetic reviewed same package";await PostDelivery(resend);
        await PostDelivery(DeliveryForm("Deliver",6));
        var deliveredHttp=await db.Orders.AsNoTracking().SingleAsync(o=>o.OrderId==deliveryOrder.OrderId);
        httpStock=await db.StockAccounts.AsNoTracking().SingleAsync(s=>s.ProductId==httpProduct.ProductId);
        check(deliveredHttp.Status=="Delivered" && deliveredHttp.StatusRevision==7 && deliveredHttp.StockState=="Filled" &&
            httpStock.OnHand==350 && httpStock.Reserved==0 && await db.OrderDeliveryEvents.CountAsync(e=>e.OrderId==deliveryOrder.OrderId)==5,
            "Real authenticated handoff-failure-receipt-redispatch-delivery keeps exact stock and audit history");
        using(var shipmentPage=await adminClient.GetAsync($"/Admin/Orders/Details/{deliveryOrder.OrderId}"))
        {
            var html=await shipmentPage.Content.ReadAsStringAsync();
            check(shipmentPage.StatusCode==HttpStatusCode.OK && html.Contains("delivery-title") && html.Contains("Synthetic HTTP carrier") && html.Contains("cookie-admin@orkidea.test"),
                "Real Razor details render delivery state, original carrier snapshot and authenticated audit actor");
        }
        using(var deliveryList=await adminClient.GetAsync($"/Admin/Delivery?state=Delivered&search=%23{deliveryOrder.OrderId}"))
            check(deliveryList.StatusCode==HttpStatusCode.OK && (await deliveryList.Content.ReadAsStringAsync()).Contains("Synthetic HTTP delivery"),
                "Real delivery list filters saved state and exact order reference");
        using(var invalidDeliveryList=await adminClient.GetAsync("/Admin/Delivery?state=unknown"))
            check(invalidDeliveryList.StatusCode==HttpStatusCode.BadRequest,"Unknown delivery-list filter is rejected");
        using(var deletedDelivery=await adminClient.PostAsync($"/Admin/Orders/Delete/{deliveryOrder.OrderId}",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",deliveryToken}})))
            check(deletedDelivery.StatusCode==HttpStatusCode.Redirect && await db.OrderDeliveries.AnyAsync(d=>d.OrderId==deliveryOrder.OrderId),
                "Real order delete action preserves registered shipment and audit trail");
        var logoutToken = await Token(adminClient, "/");
        // Real cookie + antiforgery + server session journey for manual Facebook entry.
        using(var unauthorized=await buyer.PostAsync("/Admin/ManualOrders/Create",new FormUrlEncodedContent(new Dictionary<string,string>())))
            check(Denied(unauthorized),"Customer cookie cannot submit manual Facebook orders");
        using(var missingCsrf=await adminClient.PostAsync("/Admin/ManualOrders/Create",new FormUrlEncodedContent(new Dictionary<string,string>())))
            check(missingCsrf.StatusCode==HttpStatusCode.BadRequest,"Admin manual creation requires a real CSRF token");
        var manualProduct=new Product{Name="Synthetic cookie manual product"};
        var manualVariant=new ProductVariant{Product=manualProduct,Size="small",Price=3};
        var manualOther=new ProductVariant{Product=manualProduct,Size="large",Price=5};
        var manualRate=new ShippingRate{CityName="Synthetic cookie manual city",Price=2};
        db.AddRange(manualVariant,manualOther,manualRate);await db.SaveChangesAsync();
        var manualToken=await Token(adminClient,"/Admin/ManualOrders/Create");
        var manualNonce=Guid.NewGuid();
        var manualForm=new Dictionary<string,string>{["__RequestVerificationToken"]=manualToken,["command"]="review",
            ["Input.RequestId"]=manualNonce.ToString(),["Input.CustomerName"]="Synthetic cookie manual customer",
            ["Input.Phone"]="+218000000000",["Input.Address"]="Synthetic address",["Input.ShippingRateId"]=manualRate.ShippingRateId.ToString(),
            ["Input.Lines[0].VariantId"]=manualVariant.ProductVariantId.ToString(),["Input.Lines[0].Quantity"]="2",
            ["Input.Lines[1].VariantId"]=manualOther.ProductVariantId.ToString(),["Input.Lines[1].Quantity"]="1",
            ["Input.Source"]="Website",["Input.ActorId"]="forged-admin",["Input.Lines[0].UnitPrice"]="0.01"};
        async Task<string> ManualReview()
        {
            using var response=await adminClient.PostAsync("/Admin/ManualOrders/Create",new FormUrlEncodedContent(manualForm));
            check(response.StatusCode==HttpStatusCode.OK,"Authenticated multiline manual order renders server-priced preview");
            var html=await response.Content.ReadAsStringAsync();
            var hash=Regex.Match(html,"name=\"fingerprint\"[^>]*value=\"([^\"]+)\"");
            check(hash.Success,"Manual review renders a fingerprint bound to its server draft");return hash.Groups[1].Value;
        }
        var oldFingerprint=await ManualReview();
        check(!await db.Orders.AnyAsync(o=>o.CheckoutToken==manualNonce),"HTTP manual preview creates no SQL order");
        using(var noToken=await adminClient.PostAsync("/Admin/ManualOrders/Confirm",new FormUrlEncodedContent(new Dictionary<string,string>{{"requestId",manualNonce.ToString()},{"fingerprint",oldFingerprint}})))
            check(noToken.StatusCode==HttpStatusCode.BadRequest,"Manual confirmation also requires CSRF");
        manualForm["Input.Address"]="Revised synthetic address";var fingerprint=await ManualReview();
        Dictionary<string,string> Confirmation(string hash)=>new(){["__RequestVerificationToken"]=manualToken,["requestId"]=manualNonce.ToString(),["fingerprint"]=hash};
        using(var stale=await adminClient.PostAsync("/Admin/ManualOrders/Confirm",new FormUrlEncodedContent(Confirmation(oldFingerprint))))
            check(stale.StatusCode==HttpStatusCode.Conflict && !await db.Orders.AnyAsync(o=>o.CheckoutToken==manualNonce),"Stale review tab cannot save the newer server draft without explicit review");
        using(var confirmed=await adminClient.PostAsync("/Admin/ManualOrders/Confirm",new FormUrlEncodedContent(Confirmation(fingerprint))))
            check(confirmed.StatusCode==HttpStatusCode.Redirect,"Reviewed manual order confirms through real cookie/session/CSRF host");
        db.ChangeTracker.Clear();
        var manualStored=await db.Orders.AsNoTracking().Include(o=>o.Creation).Include(o=>o.Items).SingleAsync(o=>o.CheckoutToken==manualNonce);
        check(manualStored.TotalAmount==13 && manualStored.ShippingCost==2 && manualStored.Items.Count==2 && manualStored.Items.Sum(x=>x.UnitPrice*x.Quantity)==11 &&
            manualStored.Creation?.Source==OrderSources.Facebook && manualStored.Creation.ActorUserId==adminUser.Id && manualStored.Status=="Pending",
            "Overposted source, actor and price are ignored; SQL stores authenticated creator and exact catalogue total");
        using(var replay=await adminClient.PostAsync("/Admin/ManualOrders/Confirm",new FormUrlEncodedContent(Confirmation(fingerprint))))
            check(replay.StatusCode==HttpStatusCode.Redirect && await db.Orders.CountAsync(o=>o.CheckoutToken==manualNonce)==1,"HTTP double-submit after session draft cleanup returns the original order");
        using(var details=await adminClient.GetAsync($"/Admin/Orders/Details/{manualStored.OrderId}"))
            check(details.StatusCode==HttpStatusCode.OK,"Manual order renders existing details with provenance");
        using(var list=await adminClient.GetAsync($"/Admin/Orders?source=FacebookManual&search=%23{manualStored.OrderId}"))
            check(list.StatusCode==HttpStatusCode.OK,"HTTP source-filtered manual order list renders");
        using(var exportManual=await adminClient.GetAsync($"/Admin/Orders/ExportToExcel?source=FacebookManual&search=%23{manualStored.OrderId}"))
        {
            using var workbook=new XLWorkbook(new MemoryStream(await exportManual.Content.ReadAsByteArrayAsync()));
            check(workbook.Worksheet(1).LastRowUsed()!.RowNumber()==2 && workbook.Worksheet(1).Cell(2,12).GetString()==OrderSources.Label(OrderSources.Facebook),
                "HTTP source filter and XLSX select the same manual order and appended source");
        }
        using(var deleteManual=await adminClient.PostAsync($"/Admin/Orders/Delete/{manualStored.OrderId}",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",manualToken}})))
            check(deleteManual.StatusCode==HttpStatusCode.Redirect && await db.OrderCreations.AnyAsync(c=>c.OrderId==manualStored.OrderId),"Admin deletion preserves the recorded manual provenance");

        // Anonymous customer checkout with a private, session-bound persisted summary.
        using(var noReceipt=await guest.GetAsync("/Checkout/Confirmation?orderId=1"))
            check(noReceipt.StatusCode==HttpStatusCode.Redirect,"Direct success URL without completed checkout cannot display false receipt");
        var receiptCsrf=await Token(guest,"/Identity/Account/Login");
        using(var addReceiptCart=await guest.PostAsync("/Cart/AddToCartAjax",new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"]=receiptCsrf,["variantId"]=manualVariant.ProductVariantId.ToString(),["quantity"]="2" })))
            check(addReceiptCart.StatusCode==HttpStatusCode.OK,"Anonymous receipt journey adds a real catalogue variant to its session cart");
        string receiptCheckoutKey;
        using(var checkoutPage=await guest.GetAsync("/Checkout"))
        {
            var html=await checkoutPage.Content.ReadAsStringAsync();
            var match=Regex.Match(html,"name=\"Input.CheckoutToken\"[^>]*value=\"([^\"]+)\"");
            check(checkoutPage.StatusCode==HttpStatusCode.OK && match.Success,"Anonymous checkout renders its own session-bound submission key");
            check(html.Contains("for=\"shippingRateIdInput\"",StringComparison.Ordinal) &&
                html.Contains("id=\"shippingRateIdInput\"",StringComparison.Ordinal),
                "Checkout delivery city label targets the rendered selection control");
            receiptCheckoutKey=match.Groups[1].Value;
        }
        var ordersBeforeInvalidReceipt=await db.Orders.CountAsync();
        using(var invalidReceipt=await guest.PostAsync("/Checkout/PlaceOrder",new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"]=receiptCsrf,["Input.CheckoutToken"]=receiptCheckoutKey,["Input.FirstName"]="RetainedName",
          ["Input.LastName"]="Customer",["Input.WhatsAppNumber"]="bad-phone",["Input.ShippingAddress"]="Synthetic retained address",
          ["Input.ShippingRateId"]=manualRate.ShippingRateId.ToString() })))
        {
            var invalidHtml=await invalidReceipt.Content.ReadAsStringAsync();
            check(invalidReceipt.StatusCode==HttpStatusCode.OK && invalidHtml.Contains("validation-summary-errors",StringComparison.Ordinal),
                "Invalid checkout returns an actionable error summary instead of a success receipt");
            check(Regex.IsMatch(invalidHtml,"<input[^>]*aria-describedby=\"error-WhatsAppNumber\"[^>]*aria-invalid=\"true\""),
                "Invalid phone field exposes its error association and invalid state");
            check(invalidHtml.Contains("value=\"RetainedName\"",StringComparison.Ordinal) && invalidHtml.Contains("Synthetic retained address",StringComparison.Ordinal),
                "Invalid checkout retains valid customer input for correction");
        }
        check(await db.Orders.CountAsync()==ordersBeforeInvalidReceipt,"Invalid checkout creates no order before correction");
        using(var submittedReceipt=await guest.PostAsync("/Checkout/PlaceOrder",new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"]=receiptCsrf,["Input.CheckoutToken"]=receiptCheckoutKey,["Input.FirstName"]="SyntheticReceipt",
          ["Input.LastName"]="Customer",["Input.WhatsAppNumber"]="+218000000000",["Input.ShippingAddress"]="SYNTHETIC_PRIVATE_RECEIPT_ADDRESS",
          ["Input.ShippingRateId"]=manualRate.ShippingRateId.ToString() })))
            check(submittedReceipt.StatusCode==HttpStatusCode.Redirect && submittedReceipt.Headers.Location?.OriginalString.Contains("Confirmation",StringComparison.Ordinal)==true,
                "Guest order completes without requiring account creation");
        var storedReceipt=await db.Orders.AsNoTracking().SingleAsync(o=>o.CheckoutToken==Guid.Parse(receiptCheckoutKey));
        var receiptOrderCount=await db.Orders.CountAsync();var receiptNotificationCount=await db.OrderNotifications.CountAsync();
        // A later catalogue edit must not rewrite the confirmed customer's receipt.
        await db.ProductVariants.Where(v=>v.ProductVariantId==manualVariant.ProductVariantId).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Price,999m));
        for(var repeat=0;repeat<2;repeat++)
        {
            using var receiptPage=await guest.GetAsync($"/Checkout/Confirmation?orderId={manualStored.OrderId}");
            var html=await receiptPage.Content.ReadAsStringAsync();
            check(receiptPage.StatusCode==HttpStatusCode.OK && Regex.IsMatch(html,$"data-testid=\"receipt-order-number\">#{storedReceipt.OrderId}</strong>"),
                "Receipt uses the session's stored order and ignores another order ID in the URL");
            check(html.Contains("6.00",StringComparison.Ordinal) && html.Contains("2.00",StringComparison.Ordinal) && html.Contains("8.00",StringComparison.Ordinal),
                "Receipt totals use persisted unit prices rather than the edited catalogue");
            check(!html.Contains("SYNTHETIC_PRIVATE_RECEIPT_ADDRESS",StringComparison.Ordinal) && !html.Contains("+218000000000",StringComparison.Ordinal) &&
                !html.Contains(receiptCheckoutKey,StringComparison.Ordinal),"Receipt omits customer address,phone and checkout capability");
            check(receiptPage.Headers.CacheControl?.NoStore==true && receiptPage.Headers.TryGetValues("X-Robots-Tag",out var receiptRobots) && receiptRobots.Any(h=>h.Contains("noindex",StringComparison.Ordinal)),
                "Private receipt is not cacheable or indexable");
        }
        using(var isolatedReceiptBrowser=Client())
        using(var deniedReceipt=await isolatedReceiptBrowser.GetAsync($"/Checkout/Confirmation?orderId={storedReceipt.OrderId}&CheckoutToken={receiptCheckoutKey}"))
            check(deniedReceipt.StatusCode==HttpStatusCode.Redirect,"Another cookie session cannot obtain receipt using its number or checkout token in URL");
        check(await db.Orders.CountAsync()==receiptOrderCount && await db.OrderNotifications.CountAsync()==receiptNotificationCount,
            "Receipt refresh and forged lookup URLs create no orders or notification intents");
        await db.ProductVariants.Where(v=>v.ProductVariantId==manualVariant.ProductVariantId).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Price,3m));

        using (var logout = await adminClient.PostAsync("/Identity/Account/Logout?returnUrl=%2F", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = logoutToken })))
            check(logout.StatusCode == HttpStatusCode.Redirect, "Real admin logout succeeds with antiforgery");
        using (var after = await adminClient.GetAsync("/Admin/Orders"))
            check(LoginRequired(after), "After logout old browser session cannot access admin");
    }
}
