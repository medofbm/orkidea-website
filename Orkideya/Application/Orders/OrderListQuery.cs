using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Orders;

public sealed record OrderListFilter(string Status, string Search, string Source = "all")
{
    public static bool TryCreate(string? status, string? search, out OrderListFilter filter, string? source = "all")
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
        var normalizedSearch = search?.Trim() ?? "";
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "all" : source.Trim();
        filter = new(normalizedStatus, normalizedSearch, normalizedSource);
        return normalizedStatus is "all" or "pending" or "processing" or "shipped" or "delivered" or "cancelled"
            && normalizedSearch.Length <= 100 && normalizedSource is "all" or OrderSources.Website or OrderSources.Facebook or OrderSources.Legacy;
    }
}

public sealed record OrderListPage(List<Order> Orders, int Page, int Pages, int Total, int PageSize);

public sealed class OrderListQuery(ApplicationDbContext context)
{
    public const int PageSize = 25;

    public IQueryable<Order> Matching(OrderListFilter filter)
    {
        var query = context.Orders.AsNoTracking().Include(o => o.Creation).AsQueryable();
        if (filter.Source == OrderSources.Legacy) query = query.Where(o => o.Creation == null);
        else if (filter.Source != "all") query = query.Where(o => o.Creation != null && o.Creation.Source == filter.Source);
        if (filter.Status != "all") query = query.Where(o => o.Status.ToLower() == filter.Status);
        if (filter.Search.Length != 0)
        {
            var search = filter.Search;
            var number = search.StartsWith('#') ? search[1..] : search;
            if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
                query = query.Where(o => o.OrderId == id || o.CustomerName.Contains(search)
                    || o.WhatsAppNumber.Contains(search) || o.City.Contains(search));
            else query = query.Where(o => o.CustomerName.Contains(search)
                || o.WhatsAppNumber.Contains(search) || o.City.Contains(search));
        }
        return query;
    }

    public IOrderedQueryable<Order> Ordered(OrderListFilter filter) => Matching(filter)
        .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderId);

    public async Task<OrderListPage> PageAsync(OrderListFilter filter, int page, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        var total = await Matching(filter).CountAsync(cancellationToken);
        var pages = (int)Math.Max(1L, (total + (long)PageSize - 1) / PageSize);
        var current = Math.Min(page, pages);
        var orders = await Ordered(filter).Skip((current - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        return new(orders, current, pages, total, PageSize);
    }
}
