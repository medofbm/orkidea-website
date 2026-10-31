using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkideya.Controllers;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

internal static class RoleAuthorizationRegression
{
    public static async Task Run(WebApplicationFactory<CartController> parent, Action<bool, string> check)
    {
        // Only this isolated test host knows this scheme/header. No production auth changes.
        using var factory = parent.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "RoleBoundaryTest";
                options.DefaultChallengeScheme = "RoleBoundaryTest";
                options.DefaultForbidScheme = "RoleBoundaryTest";
            }).AddScheme<AuthenticationSchemeOptions, TestRoleHandler>("RoleBoundaryTest", _ => { })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        foreach (var path in new[] { "/Admin", "/Admin/Products", "/Admin/Products/Create", "/Admin/ProductVariants",
            "/Admin/Orders", "/Admin/Orders/Details/1", "/Admin/Orders/ExportToExcel", "/Admin/Home/GetSalesChartData?days=7",
            "/Admin/Categories", "/Admin/Reviews", "/Admin/ContactMessages", "/Admin/Inventory", "/Admin/Inventory/Details/1", "/Admin/Delivery" })
        {
            using var response = await client.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.Forbidden, "Non-admin cannot read admin endpoint: " + path);
        }
        var mutations = new[] { "/Admin/Orders/UpdateStatus?id=1&status=processing", "/Admin/Orders/Delete/1",
            "/Admin/Products/Create", "/Admin/Products/Edit/1", "/Admin/ProductVariants/Create",
            "/Admin/Inventory/Open/1", "/Admin/Inventory/Configure/1", "/Admin/Inventory/Adjust/1",
            "/Admin/Inventory/OrderOperation/1", "/Admin/Inventory/Cancel/1", "/Admin/Delivery/Apply/1" };
        foreach (var path in mutations)
        {
            using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
            check(response.StatusCode == HttpStatusCode.Forbidden, "Non-admin cannot mutate admin endpoint: " + path);
        }
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        foreach (var path in mutations)
        {
            using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
            check(response.StatusCode == HttpStatusCode.BadRequest, "Admin mutation still requires antiforgery: " + path);
        }
        foreach (var days in new[] { -1, 0, 8, int.MaxValue })
        {
            using var response = await client.GetAsync("/Admin/Home/GetSalesChartData?days=" + days);
            check(response.StatusCode == HttpStatusCode.BadRequest, "Authenticated admin invalid chart range rejected: " + days);
        }
    }

    private sealed class TestRoleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (role is not ("Admin" or "Customer")) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "synthetic-role-test"),
                new Claim(ClaimTypes.Name, "Synthetic role test"), new Claim(ClaimTypes.Role, role) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
