using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

internal static class DashboardScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        await using var db = open();
        var now = new DateTime(2042, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new DashboardMetrics(db, now);
        var pendingBefore = await metrics.PendingAsync();
        check(await metrics.ThirtyDaysAsync() == 0, "Dashboard empty window has zero product value");
        Order Row(DateTime date, decimal total, decimal shipping, string status) => new()
        {
            CustomerName = "Synthetic metrics", WhatsAppNumber = "0000000000",
            ShippingAddress = "Synthetic address", City = "Synthetic", OrderDate = date,
            TotalAmount = total, ShippingCost = shipping, Status = status
        };
        db.Orders.AddRange(
            Row(now.AddHours(-1), 3, 1, "Pending"),
            Row(now.AddDays(-1), 100, 10, "Processing"),
            Row(now.AddDays(-1), 500, 10, "Cancelled"),
            Row(now.Date.AddDays(-29), 20, 5, "Delivered"),
            Row(now.Date.AddDays(-30), 1000, 1, "Delivered"),
            Row(now.Date.AddDays(1), 1000, 1, "Processing"),
            Row(now.AddDays(-40), 30, 5, "Pending"));
        await db.SaveChangesAsync();
        check(await metrics.PendingAsync() == pendingBefore + 2,
            "Pending count filters actual status and includes older unresolved orders");
        var chart = await metrics.ChartAsync(30);
        check(await metrics.ThirtyDaysAsync() == 107 && chart.Data.Sum() == 107,
            "Dashboard card/chart agree: exclude shipping, cancelled and outside calendar window");
        check(chart.Labels.Count == 30 && chart.Data.Count == 30 && chart.Data[0] == 15 && chart.Data[^1] == 2,
            "Dashboard chart includes exact first/last day and fills empty days");
        var seven = await metrics.ChartAsync(7);
        check(seven.Data.Count == 7 && seven.Data.Sum() == 92,
            "Seven-day chart uses same merchandise-value definition");
        check(await metrics.WeeklyChangeAsync() == null,
            "Zero previous value gives unavailable comparison rather than misleading zero percent");
        db.Orders.Add(Row(now.AddDays(-8), 12, 2, "Delivered"));
        await db.SaveChangesAsync();
        check(await metrics.WeeklyChangeAsync() == 820,
            "Weekly comparison uses adjacent non-overlapping windows and excludes shipping");
        var rejected = false;
        try { await metrics.ChartAsync(int.MaxValue); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "Unsupported chart ranges rejected before allocating dates");
    }
}
