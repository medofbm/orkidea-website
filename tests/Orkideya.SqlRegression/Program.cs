using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

static void Check(bool value, string description)
{
    if (!value) throw new InvalidOperationException(description);
    Console.WriteLine("PASS SQL: " + description);
}
if (Environment.GetEnvironmentVariable("ORKIDEA_RUN_SQL_TESTS") != "1")
    throw new InvalidOperationException("SQL verification requires an explicit test opt-in.");
var raw = Environment.GetEnvironmentVariable("ORKIDEA_SQL_TEST_CONNECTION")
    ?? throw new InvalidOperationException("A dedicated loopback test connection is required.");
var server = new SqlConnectionStringBuilder(raw);
if (!server.DataSource.StartsWith("127.0.0.1,", StringComparison.Ordinal) ||
    server.InitialCatalog != "master" || server.IntegratedSecurity || string.IsNullOrEmpty(server.Password))
    throw new InvalidOperationException("SQL verification only accepts the ephemeral loopback container and master bootstrap.");
for (var attempt = 0; ; attempt++)
{
    try { await using var ready = new SqlConnection(server.ConnectionString); await ready.OpenAsync(); break; }
    catch (SqlException) when (attempt < 30) { await Task.Delay(TimeSpan.FromSeconds(2)); }
}
var name = "OrkideaTest_" + Guid.NewGuid().ToString("N");
server.InitialCatalog = name;
// Identity's store options participate in EF's model; use the same registration as the app.
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<ApplicationDbContext>(builder => builder.UseSqlServer(server.ConnectionString,
    sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)));
services.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
using var provider = services.BuildServiceProvider();
var options = provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
ApplicationDbContext Open() => new(options);
Order Make(Guid token, string hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA") => new()
{
    CheckoutToken = token, CheckoutRequestHash = hash, CustomerName = "Synthetic customer",
    WhatsAppNumber = "+218000000000", ShippingAddress = "Synthetic address", City = "Tripoli",
    ShippingCost = 10m, TotalAmount = 75m, OrderDate = DateTime.UtcNow,
    Items = new() {
        new() { ProductId = 1, Quantity = 1, UnitPrice = 30m, VariantSize = "50 ml" },
        new() { ProductId = 1, Quantity = 1, UnitPrice = 35m, VariantSize = "100 ml" }
    }
};
try
{
    await using (var db = Open())
    {
        await db.GetService<IMigrator>().MigrateAsync("20260304060822_AddShippingRegionAndDuration");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Orders (CustomerName, WhatsAppNumber, ShippingAddress, City, ShippingCost, TotalAmount, OrderDate, PaymentMethod, Status) VALUES (N'Synthetic legacy', N'+218000000000', N'Test only', N'Tripoli', 10, 42.5, '2026-01-01', N'cash', N'Pending'), (N'Synthetic legacy two', N'+218000000000', N'Test only', N'Tripoli', 10, 42.5, '2026-01-01', N'cash', N'Pending')");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, VariantSize) SELECT MIN(OrderId), 1, 1, 32.5, N'legacy' FROM Orders");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, VariantSize) VALUES (2147483647, 1, 1, 1, N'ORPHAN_TEST')");
        var orphanBlocked = false;
        try { await db.Database.MigrateAsync(); }
        catch (SqlException exception) when (exception.Number == 51001) { orphanBlocked = true; }
        Check(orphanBlocked, "Upgrade stops explicitly when legacy orphan items exist");
        Check(await db.OrderItems.CountAsync() == 2, "Blocked upgrade preserves every legacy row for reconciliation");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM OrderItems WHERE OrderId = 2147483647 AND VariantSize = N'ORPHAN_TEST'");
        await db.Database.MigrateAsync();
        Check(await db.Orders.CountAsync(order => order.CheckoutToken == null) == 2, "Upgrade retains legacy orders and permits multiple null retry keys");
        Check(await db.OrderItems.CountAsync() == 1, "Upgrade retains legacy order items");
        Check(!db.Database.HasPendingModelChanges(), "Committed migration snapshot matches the EF model");
        await db.Database.MigrateAsync();
        Check(await db.Orders.CountAsync() == 2, "Repeating migrations preserves existing orders");
        db.ShippingRates.Add(new ShippingRate { CityName = "طرابلس", Price = 17.25m });
        await db.SaveChangesAsync();
        await DbInitializer.SeedShippingRatesAsync(db);
        var rateCount = await db.ShippingRates.CountAsync();
        await DbInitializer.SeedShippingRatesAsync(db);
        Check(await db.ShippingRates.CountAsync() == rateCount, "Repeated shipping initialization adds no duplicates");
        Check((await db.ShippingRates.SingleAsync(rate => rate.CityName == "طرابلس")).Price == 17.25m, "Shipping initialization preserves configured prices");
    }
    var token = Guid.NewGuid();
    int firstId;
    await using (var db = Open())
    {
        var placed = await new OrderService(db).PlaceAsync(Make(token));
        firstId = placed.Order.OrderId;
        Check(placed.IsNew, "First request creates an order");
    }
    await using (var db = Open())
    {
        Check(await db.OrderItems.CountAsync(item => item.OrderId == firstId) == 2, "Order and all items persist together");
        var placed = await new OrderService(db).PlaceAsync(Make(token));
        Check(!placed.IsNew && placed.Order.OrderId == firstId, "Retry returns the original order without a new notification event");
        Check(await db.Orders.CountAsync(order => order.CheckoutToken == token) == 1, "Retry does not duplicate the order");
        var conflicted = false;
        try { await new OrderService(db).PlaceAsync(Make(token, new string('B', 64))); }
        catch (CheckoutConflictException) { conflicted = true; }
        Check(conflicted, "Retry key with different request data is rejected");
    }
    var concurrentToken = Guid.NewGuid();
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
    {
        await using var db = Open();
        return await new OrderService(db).PlaceAsync(Make(concurrentToken));
    }));
    Check(concurrent.Count(result => result.IsNew) == 1, "Eight simultaneous requests create exactly one new order");
    Check(concurrent.Select(result => result.Order.OrderId).Distinct().Count() == 1, "Concurrent retries return the same order identity");
    await using (var db = Open())
    {
        Check(await db.Orders.CountAsync(order => order.CheckoutToken == concurrentToken) == 1, "Unique retry key is enforced by SQL Server");
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectSyntheticItem ON dbo.OrderItems AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE VariantSize = N'FAIL_TEST') THROW 51000, 'Synthetic item failure', 1; END");
    }
    var failedToken = Guid.NewGuid();
    await using (var db = Open())
    {
        var failed = Make(failedToken); failed.Items[1].VariantSize = "FAIL_TEST";
        var rejected = false;
        try { await new OrderService(db).PlaceAsync(failed); }
        catch (DbUpdateException) { rejected = true; }
        Check(rejected, "SQL failure during item insertion reaches the caller");
    }
    await using (var db = Open())
    {
        Check(!await db.Orders.AnyAsync(order => order.CheckoutToken == failedToken), "Failed item rolls back its parent order");
        Check(!await db.OrderItems.AnyAsync(item => !db.Orders.Any(order => order.OrderId == item.OrderId)), "Failed item leaves no orphan or partial rows");
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectSyntheticItem");
        Check((await new OrderService(db).PlaceAsync(Make(failedToken))).IsNew, "A failed request can retry safely after recovery");
    }
    await using (var db = Open()) await CheckoutScenario.Run(db, Check);
    Console.WriteLine("SQL SERVER VERIFICATION COMPLETE: dedicated generated test database only.");
}
finally
{
    // This name was generated above, never supplied by a user or configuration.
    if (!name.StartsWith("OrkideaTest_", StringComparison.Ordinal) || name.Length != 44)
        throw new InvalidOperationException("Refusing test cleanup outside the generated database.");
    await using var cleanup = Open();
    await cleanup.Database.EnsureDeletedAsync();
}
