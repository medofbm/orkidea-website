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
        foreach (var path in new[] { "/Admin", "/Admin/Orders", "/Admin/Orders/ExportToExcel" })
        {
            using var response = await guest.GetAsync(path);
            check(LoginRequired(response), "Anonymous real-cookie host cannot read admin: " + path);
        }
        using var buyer = Client();
        await Login(buyer, customer.Email!);
        using (var manage = await buyer.GetAsync("/Identity/Account/Manage"))
            check(manage.StatusCode == HttpStatusCode.OK, "Customer cookie accesses own account");
        foreach (var path in new[] { "/Admin", "/Admin/Products", "/Admin/Orders/Details/1", "/Admin/Orders/ExportToExcel" })
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
        var audited = await db.Orders.AsNoTracking().FirstAsync(order => order.Status == "Pending");
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
        var logoutToken = await Token(adminClient, "/");
        using (var logout = await adminClient.PostAsync("/Identity/Account/Logout?returnUrl=%2F", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = logoutToken })))
            check(logout.StatusCode == HttpStatusCode.Redirect, "Real admin logout succeeds with antiforgery");
        using (var after = await adminClient.GetAsync("/Admin/Orders"))
            check(LoginRequired(after), "After logout old browser session cannot access admin");
    }
}
