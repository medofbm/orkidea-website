using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Areas.Admin.Models;

namespace Orkideya.Application.Orders;

// Ordered merchandise value, not revenue received or the courier's balance.
public sealed class DashboardMetrics(ApplicationDbContext context, DateTime nowUtc)
{
    private IQueryable<Order> Orders(DateTime start, DateTime end) => context.Orders.AsNoTracking()
        .Where(o => o.Status != "Cancelled" && o.OrderDate >= start && o.OrderDate < end);

    public Task<decimal> ValueAsync(DateTime start, DateTime end) => Orders(start, end)
        .SumAsync(o => o.TotalAmount - o.ShippingCost);

    public Task<int> PendingAsync() => context.Orders.CountAsync(o => o.Status == "Pending");

    public async Task<decimal?> WeeklyChangeAsync()
    {
        var current = await ValueAsync(nowUtc.AddDays(-7), nowUtc);
        var previous = await ValueAsync(nowUtc.AddDays(-14), nowUtc.AddDays(-7));
        return previous == 0 ? null : (current - previous) / previous * 100;
    }

    public async Task<SalesChartDataViewModel> ChartAsync(int days)
    {
        if (days is not (7 or 30)) throw new ArgumentOutOfRangeException(nameof(days));
        var start = nowUtc.Date.AddDays(1 - days);
        var end = nowUtc.Date.AddDays(1);
        var values = await Orders(start, end).GroupBy(o => o.OrderDate.Date)
            .Select(g => new { Date = g.Key, Value = g.Sum(o => o.TotalAmount - o.ShippingCost) })
            .ToDictionaryAsync(x => x.Date, x => x.Value);
        var dates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToArray();
        return new SalesChartDataViewModel
        {
            Labels = dates.Select(d => d.ToString("dd/MM")).ToList(),
            Data = dates.Select(d => values.GetValueOrDefault(d)).ToList()
        };
    }

    public Task<decimal> ThirtyDaysAsync() => ValueAsync(nowUtc.Date.AddDays(-29), nowUtc.Date.AddDays(1));

    // Non-cancelled orders in the same 30-day calendar window as ThirtyDaysAsync.
    public Task<int> ThirtyDayOrdersAsync() => Orders(nowUtc.Date.AddDays(-29), nowUtc.Date.AddDays(1)).CountAsync();

    // Today's non-cancelled orders and their merchandise value (UTC calendar day, like the chart).
    public async Task<(int Orders, decimal Value)> TodayAsync()
    {
        var today = Orders(nowUtc.Date, nowUtc.Date.AddDays(1));
        return (await today.CountAsync(), await today.SumAsync(o => o.TotalAmount - o.ShippingCost));
    }

    // All-time order count per status, for the dashboard status strip.
    public Task<Dictionary<string, int>> StatusCountsAsync() => context.Orders.AsNoTracking()
        .GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.Key, x => x.Count);

    // Best sellers by quantity over the 30-day window, cancelled orders excluded.
    public async Task<IReadOnlyList<TopProduct>> TopProductsAsync(int take)
    {
        var start = nowUtc.Date.AddDays(-29);
        var end = nowUtc.Date.AddDays(1);
        var rows = await (from item in context.OrderItems.AsNoTracking()
                          join order in Orders(start, end) on item.OrderId equals order.OrderId
                          group item by item.ProductId into g
                          select new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity), Value = g.Sum(i => i.Quantity * i.UnitPrice) })
            .OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Value).Take(take).ToListAsync();
        var ids = rows.Select(r => r.ProductId).ToList();
        var names = await context.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.Name);
        return rows.Select(r => new TopProduct(r.ProductId, names.GetValueOrDefault(r.ProductId), r.Quantity, r.Value)).ToList();
    }
}

public sealed record TopProduct(int ProductId, string? Name, int Quantity, decimal Value);
