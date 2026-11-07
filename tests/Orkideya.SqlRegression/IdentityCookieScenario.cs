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
using System.Net.Http.Json;
using System.Text.Json;
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
        using (var apiBuyer = Client())
        {
            using var session = await apiBuyer.GetAsync("/api/v1/session");
            var tokenPayload = await session.Content.ReadFromJsonAsync<JsonElement>();
            var apiToken = tokenPayload.GetProperty("requestToken").GetString();
            check(session.Headers.CacheControl?.NoStore == true, "API token response disables caching");
            var variantId = await db.ProductVariants.Where(v => v.Price > 0).Select(v => v.ProductVariantId).FirstAsync();
            using var unsafeAdd = await apiBuyer.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity = 1 });
            check(unsafeAdd.StatusCode == HttpStatusCode.BadRequest, "Cart API rejects a write without antiforgery");
            apiBuyer.DefaultRequestHeaders.Add("RequestVerificationToken", apiToken);
            using var add = await apiBuyer.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity = 2 });
            var added = await add.Content.ReadFromJsonAsync<JsonElement>();
            check(add.StatusCode == HttpStatusCode.OK && added.GetProperty("success").GetBoolean(),
                "Cart API adds an existing variant with real session and antiforgery cookies");
            using var state = await apiBuyer.GetAsync("/api/v1/cart");
            var cartPayload = await state.Content.ReadFromJsonAsync<JsonElement>();
            check(cartPayload.GetProperty("count").GetInt32() == 2 && state.Headers.CacheControl?.NoStore == true,
                "Cart API reads the same private session cart");
            using var invalid = await apiBuyer.PatchAsJsonAsync("/api/v1/cart/items/" + variantId, new { variantId, quantity = 100 });
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "Cart API enforces the quantity limit");
            using var update = await apiBuyer.PatchAsJsonAsync("/api/v1/cart/items/" + variantId, new { variantId, quantity = 3 });
            check(update.StatusCode == HttpStatusCode.OK, "Cart API updates the selected quantity");
            using var delete = await apiBuyer.DeleteAsync("/api/v1/cart/items/" + variantId);
            using var empty = await apiBuyer.GetAsync("/api/v1/cart");
            var emptyPayload = await empty.Content.ReadFromJsonAsync<JsonElement>();
            check(delete.StatusCode == HttpStatusCode.OK && emptyPayload.GetProperty("count").GetInt32() == 0,
                "Cart API removes the selected variant");
        }
        using (var accountClient = Client())
        {
            var credentials = new { email = adminUser.Email, password, rememberMe = false };
            using var rejected = await accountClient.PostAsJsonAsync("/api/v1/account/login", credentials);
            check(rejected.StatusCode == HttpStatusCode.BadRequest, "Account API rejects login without antiforgery");
            var session = await accountClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
            accountClient.DefaultRequestHeaders.Add("RequestVerificationToken", session.GetProperty("requestToken").GetString());
            using var login = await accountClient.PostAsJsonAsync("/api/v1/account/login", credentials);
            check(login.StatusCode == HttpStatusCode.OK && login.Headers.TryGetValues("Set-Cookie", out var authCookies) &&
                authCookies.Any(cookie => cookie.Contains("Identity.Application=", StringComparison.Ordinal) &&
                    cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase) &&
                    cookie.Contains("; httponly", StringComparison.OrdinalIgnoreCase)),
                "Account API login issues the existing Secure HttpOnly Identity cookie");
            using var account = await accountClient.GetAsync("/api/v1/account");
            var signedIn = await account.Content.ReadFromJsonAsync<JsonElement>();
            check(account.Headers.CacheControl?.NoStore == true &&
                signedIn.GetProperty("signedIn").GetBoolean() && signedIn.GetProperty("isAdmin").GetBoolean(),
                "Account API resolves the authenticated role without caching");
            using var rejectedLogout = await accountClient.PostAsync("/api/v1/account/logout", null);
            check(rejectedLogout.StatusCode == HttpStatusCode.BadRequest, "Account API rejects logout using the pre-login token");
            accountClient.DefaultRequestHeaders.Remove("RequestVerificationToken");
            session = await accountClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
            accountClient.DefaultRequestHeaders.Add("RequestVerificationToken", session.GetProperty("requestToken").GetString());
            using var logoutApi = await accountClient.PostAsync("/api/v1/account/logout", null);
            var signedOut = await accountClient.GetFromJsonAsync<JsonElement>("/api/v1/account");
            check(logoutApi.StatusCode == HttpStatusCode.NoContent && !signedOut.GetProperty("signedIn").GetBoolean(),
                "Account API logout removes authenticated access");
        }

        var twoFactorUser = await Seed("cookie-two-factor@orkidea.test", false);
        check((await users.ResetAuthenticatorKeyAsync(twoFactorUser)).Succeeded &&
            (await users.SetTwoFactorEnabledAsync(twoFactorUser, true)).Succeeded, "Synthetic account enables two-factor authentication");
        var recoveryCode = (await users.GenerateNewTwoFactorRecoveryCodesAsync(twoFactorUser, 1))!.Single();
        using (var twoFactorClient = Client())
        {
            var session = await twoFactorClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
            twoFactorClient.DefaultRequestHeaders.Add("RequestVerificationToken", session.GetProperty("requestToken").GetString());
            using var login = await twoFactorClient.PostAsJsonAsync("/api/v1/account/login",
                new { email = twoFactorUser.Email, password, rememberMe = false });
            var challenge = await login.Content.ReadFromJsonAsync<JsonElement>();
            var pendingAccount = await twoFactorClient.GetFromJsonAsync<JsonElement>("/api/v1/account");
            check(challenge.GetProperty("requiresVerification").GetBoolean() && !pendingAccount.GetProperty("signedIn").GetBoolean(),
                "Password alone does not authenticate an account with two-factor enabled");
            using var verified = await twoFactorClient.PostAsJsonAsync("/api/v1/account/verify",
                new { code = recoveryCode, recovery = true, rememberMe = false });
            var signedIn = await twoFactorClient.GetFromJsonAsync<JsonElement>("/api/v1/account");
            check(verified.StatusCode == HttpStatusCode.OK && signedIn.GetProperty("signedIn").GetBoolean() &&
                !signedIn.GetProperty("isAdmin").GetBoolean(), "Recovery code completes the Identity challenge with customer permissions");
        }

        using (var profileClient = Client())
        {
            const string email = "vue-profile-customer@orkidea.test";
            using var anonymousWrite = await profileClient.PostAsJsonAsync("/api/v1/account/phone",
                new { phoneNumber = "+218000000001" });
            check(anonymousWrite.StatusCode == HttpStatusCode.Unauthorized,
                "Anonymous API callers cannot update a profile");
            async Task RefreshProfileToken()
            {
                profileClient.DefaultRequestHeaders.Remove("RequestVerificationToken");
                var session = await profileClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
                profileClient.DefaultRequestHeaders.Add("RequestVerificationToken", session.GetProperty("requestToken").GetString());
            }
            await RefreshProfileToken();
            using var mismatch = await profileClient.PostAsJsonAsync("/api/v1/account/register",
                new { email, password, confirmation = password + "x" });
            check(mismatch.StatusCode == HttpStatusCode.BadRequest && await users.FindByEmailAsync(email) is null,
                "Registration with mismatched passwords creates no account");
            using var registered = await profileClient.PostAsJsonAsync("/api/v1/account/register",
                new { email, password, confirmation = password });
            var newSession = await profileClient.GetFromJsonAsync<JsonElement>("/api/v1/account");
            check(registered.StatusCode == HttpStatusCode.NoContent && newSession.GetProperty("signedIn").GetBoolean() &&
                !newSession.GetProperty("isAdmin").GetBoolean(), "Registration creates a signed-in customer without admin privileges");
            await RefreshProfileToken();
            using var phoneUpdate = await profileClient.PostAsJsonAsync("/api/v1/account/phone",
                new { phoneNumber = "+218000000001" });
            var profile = await profileClient.GetFromJsonAsync<JsonElement>("/api/v1/account");
            check(phoneUpdate.StatusCode == HttpStatusCode.NoContent &&
                profile.GetProperty("phoneNumber").GetString() == "+218000000001",
                "Phone update persists on the authenticated profile");
            var newPassword = password + "New1!";
            using var badPassword = await profileClient.PostAsJsonAsync("/api/v1/account/password",
                new { currentPassword = "Incorrect", newPassword, confirmation = newPassword });
            check(badPassword.StatusCode == HttpStatusCode.BadRequest,
                "Password change requires the current password");
            using var passwordUpdate = await profileClient.PostAsJsonAsync("/api/v1/account/password",
                new { currentPassword = password, newPassword, confirmation = newPassword });
            check(passwordUpdate.StatusCode == HttpStatusCode.NoContent, "Password change succeeds with the current password");
            using var loginClient = Client();
            var loginSession = await loginClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
            loginClient.DefaultRequestHeaders.Add("RequestVerificationToken", loginSession.GetProperty("requestToken").GetString());
            using var oldLogin = await loginClient.PostAsJsonAsync("/api/v1/account/login", new { email, password, rememberMe = false });
            using var newLogin = await loginClient.PostAsJsonAsync("/api/v1/account/login",
                new { email, password = newPassword, rememberMe = false });
            check(oldLogin.StatusCode == HttpStatusCode.Unauthorized && newLogin.StatusCode == HttpStatusCode.OK,
                "Only the new password can authenticate after the update");
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
        var fulfillmentPath = $"/api/v1/admin/orders/{deliveryOrder.OrderId}";
        var fulfillmentSession = await adminClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
        var fulfillmentToken = fulfillmentSession.GetProperty("requestToken").GetString()!;
        async Task<HttpStatusCode> PostFulfillment<T>(string action, T input)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, fulfillmentPath + "/" + action)
                { Content = JsonContent.Create(input) };
            request.Headers.Add("RequestVerificationToken", fulfillmentToken);
            using var response = await adminClient.SendAsync(request);
            return response.StatusCode;
        }
        var reserveInput = new { operation = "Reserve", expectedRevision = 0 };
        using (var forbiddenStock = await buyer.PostAsJsonAsync(fulfillmentPath + "/stock", reserveInput))
            check(forbiddenStock.StatusCode == HttpStatusCode.Forbidden, "Customer cannot use the stock JSON endpoint");
        using (var missingStockToken = await adminClient.PostAsJsonAsync(fulfillmentPath + "/stock", reserveInput))
            check(missingStockToken.StatusCode == HttpStatusCode.BadRequest, "Stock JSON endpoint requires antiforgery");
        check(await PostFulfillment("stock", reserveInput) == HttpStatusCode.OK &&
            await PostFulfillment("stock", new { operation = "Consume", expectedRevision = 0 }) == HttpStatusCode.Conflict &&
            await PostFulfillment("stock", new { operation = "Consume", expectedRevision = 1 }) == HttpStatusCode.OK,
            "Stock JSON reserves, rejects a stale fill and consumes the current reservation");
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
        var handoffInput = new
        {
            action = "HandOver", expectedRevision = 2, requestId = httpHandoff["RequestId"],
            mode = "Company", carrierName = httpHandoff["CarrierName"], shipmentReference = httpHandoff["ShipmentReference"]
        };
        using (var forbiddenDelivery = await buyer.PostAsJsonAsync(fulfillmentPath + "/delivery", handoffInput))
            check(forbiddenDelivery.StatusCode == HttpStatusCode.Forbidden, "Customer cannot use the delivery JSON endpoint");
        using (var missingDeliveryToken = await adminClient.PostAsJsonAsync(fulfillmentPath + "/delivery", handoffInput))
            check(missingDeliveryToken.StatusCode == HttpStatusCode.BadRequest, "Delivery JSON endpoint requires antiforgery");
        check(await PostFulfillment("delivery", handoffInput) == HttpStatusCode.OK &&
            await PostFulfillment("delivery", handoffInput) == HttpStatusCode.OK &&
            await PostFulfillment("delivery", new
            {
                action = "MarkReturning", expectedRevision = 2, requestId = Guid.NewGuid(), reason = "Synthetic stale JSON"
            }) == HttpStatusCode.Conflict, "Delivery JSON accepts identical replay and rejects a stale action");
        await PostDelivery(httpHandoff);
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
        check(await PostFulfillment("delivery", new
        {
            action = "ReceiveReturn", expectedRevision = 4, requestId = returnedDelivery["RequestId"],
            confirmReturnReceived = true
        }) == HttpStatusCode.OK, "Delivery JSON registers physical receipt of a returned package");
        var resend=DeliveryForm("Redispatch",5);resend["Mode"]="Self";resend["ConfirmSamePackageReusable"]="true";resend["Reason"]="Synthetic reviewed same package";await PostDelivery(resend);
        await PostDelivery(DeliveryForm("Deliver",6));
        var deliveredDetail = await adminClient.GetFromJsonAsync<JsonElement>(fulfillmentPath);
        var shipment = deliveredDetail.GetProperty("delivery");
        var shipmentHistory = deliveredDetail.GetProperty("deliveryHistory");
        check(shipment.GetProperty("state").GetString() == "Delivered" &&
            shipment.GetProperty("mode").GetString() == "Self" &&
            shipmentHistory.GetArrayLength() == 5 &&
            shipmentHistory[0].GetProperty("action").GetString() == "Deliver" &&
            shipmentHistory[4].GetProperty("actorName").GetString() == adminUser.UserName &&
            !shipmentHistory[4].TryGetProperty("requestHash", out _),
            "Admin order JSON returns the latest shipment and its private bounded audit history");
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

        var noOptionsProduct=new Product{Name="Synthetic product with no options"};
        db.Products.Add(noOptionsProduct);await db.SaveChangesAsync();
        using(var noOptionsPage=await guest.GetAsync($"/Home/ProductDetail/{noOptionsProduct.ProductId}"))
        {
            var html=await noOptionsPage.Content.ReadAsStringAsync();
            check(noOptionsPage.StatusCode==HttpStatusCode.OK && html.Contains("data-testid=\"product-options-unavailable\"",StringComparison.Ordinal),
                "Product without sizes renders an explicit unavailable state instead of a server error");
            check(!html.Contains("id=\"detailAddToCart\"",StringComparison.Ordinal) && !html.Contains("id=\"detailPrice\"",StringComparison.Ordinal),
                "Product without sizes cannot display a fictitious price or add-to-cart control");
        }
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
            check(checkoutPage.Headers.CacheControl?.NoStore==true,"Checkout input page prohibits browser/proxy caching");
            receiptCheckoutKey=match.Groups[1].Value;
        }
        var ordersBeforeInvalidReceipt=await db.Orders.CountAsync();
        using(var invalidReceipt=await guest.PostAsync("/Checkout/PlaceOrder",new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"]=receiptCsrf,["Input.CheckoutToken"]=receiptCheckoutKey,["Input.FirstName"]="RetainedName",
          ["Input.LastName"]="Customer",["Input.WhatsAppNumber"]="bad-phone",["Input.ShippingAddress"]="Synthetic retained address",
          ["Input.ShippingRateId"]=manualRate.ShippingRateId.ToString() })))
        {
            var invalidHtml=await invalidReceipt.Content.ReadAsStringAsync();
            check(invalidReceipt.Headers.CacheControl?.NoStore==true,"Validation response containing customer inputs prohibits caching");
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

        using (var apiCheckout = Client())
        {
            var session = await apiCheckout.GetFromJsonAsync<JsonElement>("/api/v1/session");
            apiCheckout.DefaultRequestHeaders.Add("RequestVerificationToken", session.GetProperty("requestToken").GetString());
            using var added = await apiCheckout.PostAsJsonAsync("/api/v1/cart/items",
                new { variantId = manualVariant.ProductVariantId, quantity = 2 });
            var addedCart = await added.Content.ReadFromJsonAsync<JsonElement>();
            check(addedCart.GetProperty("success").GetBoolean(), "Vue checkout journey shares the cart session");
            var checkout = await apiCheckout.GetFromJsonAsync<JsonElement>("/api/v1/checkout");
            var key = checkout.GetProperty("checkoutToken").GetGuid();
            var revision = checkout.GetProperty("cartRevision").GetString();
            var input = new CheckoutInput
            {
                CheckoutToken = key, FirstName = "Synthetic", LastName = "API receipt",
                WhatsAppNumber = "+218000000000", ShippingAddress = "SYNTHETIC_API_PRIVATE_ADDRESS",
                ShippingRateId = manualRate.ShippingRateId
            };
            var before = await db.Orders.CountAsync();
            using var stale = await apiCheckout.PostAsJsonAsync("/api/v1/checkout/orders",
                new { input, review = new { cartRevision = revision, shippingFee = "0.00" } });
            var corrected = await stale.Content.ReadFromJsonAsync<JsonElement>();
            check(stale.StatusCode == HttpStatusCode.UnprocessableEntity && await db.Orders.CountAsync() == before &&
                corrected.GetProperty("shippingFee").GetString() == "2.00",
                "Checkout API requires review when delivery cost differs from the displayed amount");
            var order = new { input, review = new { cartRevision = revision, shippingFee = "2.00" } };
            using var confirmed = await apiCheckout.PostAsJsonAsync("/api/v1/checkout/orders", order);
            var receiptBody = await confirmed.Content.ReadAsStringAsync();
            using var receiptJson = JsonDocument.Parse(receiptBody);
            check(confirmed.StatusCode == HttpStatusCode.OK && receiptJson.RootElement.GetProperty("total").GetString() == "8.00",
                "Checkout API creates one guest order with the confirmed totals");
            check(confirmed.Headers.CacheControl?.NoStore == true &&
                !receiptBody.Contains(input.ShippingAddress, StringComparison.Ordinal) &&
                !receiptBody.Contains(input.WhatsAppNumber, StringComparison.Ordinal) &&
                !receiptBody.Contains(key.ToString(), StringComparison.Ordinal),
                "Checkout API response keeps receipt private");
            using var replay = await apiCheckout.PostAsJsonAsync("/api/v1/checkout/orders", order);
            check(replay.StatusCode == HttpStatusCode.OK && await db.Orders.CountAsync() == before + 1,
                "Checkout API retries return the same receipt without duplicate orders");
            using var unrelated = Client();
            using var denied = await unrelated.GetAsync("/api/v1/checkout/receipt?orderId=" +
                receiptJson.RootElement.GetProperty("orderId").GetInt32());
            check(denied.StatusCode == HttpStatusCode.NotFound, "Another session cannot read the JSON receipt");
        }

        foreach (var path in new[] { "/api/v1/admin/dashboard", "/api/v1/admin/orders", "/api/v1/admin/orders/" + manualStored.OrderId })
        {
            using var anonymousAdmin = Client();
            using var deniedAnonymous = await anonymousAdmin.GetAsync(path);
            using var deniedCustomer = await buyer.GetAsync(path);
            check(deniedAnonymous.StatusCode == HttpStatusCode.Unauthorized &&
                deniedCustomer.StatusCode == HttpStatusCode.Forbidden, "Admin read API enforces the Admin role: " + path);
        }

        using (var overview = await adminClient.GetAsync("/api/v1/admin/dashboard"))
        {
            var summary = await overview.Content.ReadFromJsonAsync<JsonElement>();
            var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var expected = await new DashboardMetrics(db, nowUtc).ThirtyDaysAsync();
            check(overview.StatusCode == HttpStatusCode.OK && overview.Headers.CacheControl?.NoStore == true &&
                summary.GetProperty("productsValueLast30Days").GetString() == expected.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                "Admin dashboard API uses the existing merchandise metric and private cache headers");
        }
        using (var orderList = await adminClient.GetAsync(
            "/api/v1/admin/orders?source=FacebookManual&search=%23" + manualStored.OrderId))
        {
            var list = await orderList.Content.ReadFromJsonAsync<JsonElement>();
            var row = list.GetProperty("items")[0];
            check(orderList.StatusCode == HttpStatusCode.OK && orderList.Headers.CacheControl?.NoStore == true &&
                list.GetProperty("total").GetInt32() == 1 && row.GetProperty("id").GetInt32() == manualStored.OrderId &&
                !row.TryGetProperty("shippingAddress", out _) && !row.TryGetProperty("checkoutToken", out _),
                "Admin list API filters by source and search and limits the summary fields");
        }
        using (var badFilter = await adminClient.GetAsync("/api/v1/admin/orders?source=unknown&page=0"))
            check(badFilter.StatusCode == HttpStatusCode.BadRequest, "Admin list API rejects invalid filter input");
        using (var orderDetail = await adminClient.GetAsync("/api/v1/admin/orders/" + manualStored.OrderId))
        {
            var detail = await orderDetail.Content.ReadFromJsonAsync<JsonElement>();
            check(orderDetail.StatusCode == HttpStatusCode.OK && orderDetail.Headers.CacheControl?.NoStore == true &&
                detail.GetProperty("order").GetProperty("id").GetInt32() == manualStored.OrderId &&
                detail.GetProperty("items").GetArrayLength() == manualStored.Items.Count && detail.TryGetProperty("shippingAddress", out _),
                "Admin detail API exposes the selected order and its items to the administrator");
        }

        var updatePath = "/api/v1/admin/orders/" + manualStored.OrderId + "/status";
        var statusPayload = new { status = "Processing", expectedRevision = manualStored.StatusRevision };
        using (var denied = await buyer.PostAsJsonAsync(updatePath, statusPayload))
            check(Denied(denied), "Customer role cannot update orders through the Vue API");
        using (var missingToken = await adminClient.PostAsJsonAsync(updatePath, statusPayload))
            check(missingToken.StatusCode == HttpStatusCode.BadRequest, "Admin status update requires antiforgery");

        var adminSession = await adminClient.GetFromJsonAsync<JsonElement>("/api/v1/session");
        var adminRequestToken = adminSession.GetProperty("requestToken").GetString()!;
        async Task<HttpResponseMessage> SendUpdate<T>(T payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, updatePath) { Content = JsonContent.Create(payload) };
            request.Headers.Add("RequestVerificationToken", adminRequestToken);
            return await adminClient.SendAsync(request);
        }

        using (var invalid = await SendUpdate(new { status = "Processing" }))
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "Admin status update rejects missing revision");
        using (var saved = await SendUpdate(statusPayload))
        {
            var result = await saved.Content.ReadFromJsonAsync<JsonElement>();
            check(saved.StatusCode == HttpStatusCode.OK && result.GetProperty("success").GetBoolean(),
                "Admin status API applies the current revision");
        }
        using (var stale = await SendUpdate(statusPayload))
            check(stale.StatusCode == HttpStatusCode.Conflict, "Admin status API rejects stale writes");
        using (var updated = await adminClient.GetAsync("/api/v1/admin/orders/" + manualStored.OrderId))
        {
            var detail = await updated.Content.ReadFromJsonAsync<JsonElement>();
            var history = detail.GetProperty("history");
            check(detail.GetProperty("order").GetProperty("status").GetString() == "Processing" &&
                detail.GetProperty("statusRevision").GetInt32() == manualStored.StatusRevision + 1 &&
                history.GetArrayLength() == 1 && history[0].GetProperty("previousStatus").GetString() == "Pending" &&
                history[0].GetProperty("newStatus").GetString() == "Processing" &&
                history[0].GetProperty("actorName").GetString() == adminUser.UserName,
                "Admin status and audit history are persisted exactly once with the authenticated actor");
        }

        using (var logout = await adminClient.PostAsync("/Identity/Account/Logout?returnUrl=%2F", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = logoutToken })))
            check(logout.StatusCode == HttpStatusCode.Redirect, "Real admin logout succeeds with antiforgery");
        using (var after = await adminClient.GetAsync("/Admin/Orders"))
            check(LoginRequired(after), "After logout old browser session cannot access admin");
    }
}
