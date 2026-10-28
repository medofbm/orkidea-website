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
}
