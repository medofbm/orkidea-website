using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orkideya.Controllers;
using Orkideya.Data;
using System.Data.Common;
using System.Net;

internal static class HttpSecurityRegression
{
    public static async Task Run(Action<bool, string> check)
    {
        // Any web-assembly type identifies the application's entry assembly;
        // the console runner also has its own generated Program type.
        using var factory = new WebApplicationFactory<CartController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Initialization:Run"] = "false",
                    ["Notifications:WhatsAppEnabled"] = "false",
                    ["AllowedHosts"] = "localhost",
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddDbContext<ApplicationDbContext>(options => options
                    .UseSqlServer("Server=127.0.0.1,1;Database=OrkideaHttpTests;Integrated Security=true;Connect Timeout=1")
                    .AddInterceptors(new RejectDatabaseConnections()));
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        foreach (var path in new[] { "/Admin/Products", "/Admin/Orders", "/Admin/Orders/ExportToExcel" })
        {
            using var response = await client.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.Redirect &&
                response.Headers.Location?.OriginalString.Contains("/Identity/Account/Login", StringComparison.Ordinal) == true,
                "Anonymous access requires login: " + path);
        }
        foreach (var path in new[] { "/orders.xlsx", "/private/orders.XLSX" })
        {
            using var response = await client.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.NotFound, "Public workbook blocked: " + path);
        }
        foreach (var path in new[] { "/Cart/IncreaseQuantity", "/Cart/RemoveFromCart", "/Checkout/PlaceOrder" })
        {
            using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
            check(response.StatusCode == HttpStatusCode.BadRequest, "Missing antiforgery token rejected: " + path);
        }
        using var getMutation = await client.GetAsync("/Cart/IncreaseQuantity?id=1");
        check(getMutation.StatusCode == HttpStatusCode.MethodNotAllowed, "GET cannot increase cart quantity");
    }

    private sealed class RejectDatabaseConnections : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result) => throw new InvalidOperationException("HTTP security tests must not access a database.");
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("HTTP security tests must not access a database.");
    }
}
