using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;

namespace Orkideya.Application.Delivery;

public sealed class DeliveryListQuery(ApplicationDbContext db)
{
    private static readonly string[] States = ["active", "all", "HandedOver", "Returning", "Returned", "Delivered"];
    private const int PageSize = 25;

    public async Task<DeliveryListPage?> PageAsync(string state, string search, int page, CancellationToken ct)
    {
        var canonical = States.FirstOrDefault(value => value.Equals(state?.Trim(), StringComparison.OrdinalIgnoreCase));
        search = search?.Trim() ?? "";
        if (canonical is null || search.Length > 100 || page < 1) return null;

        var query = from shipment in db.OrderDeliveries.AsNoTracking()
                    join order in db.Orders.AsNoTracking() on shipment.OrderId equals order.OrderId
                    select new DeliveryListRow
                    {
                        OrderId = order.OrderId, CustomerName = order.CustomerName, City = order.City,
                        Mode = shipment.Mode, CarrierName = shipment.CarrierName, ShipmentReference = shipment.ShipmentReference,
                        State = shipment.State, UpdatedAtUtc = shipment.UpdatedAtUtc
                    };
        if (canonical == "active") query = query.Where(row => row.State == "HandedOver" || row.State == "Returning");
        else if (canonical != "all") query = query.Where(row => row.State == canonical);

        if (search.Length > 0)
        {
            var number = search.StartsWith('#') ? search[1..] : search;
            var orderId = int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            query = query.Where(row => row.OrderId == orderId || row.CustomerName.Contains(search) || row.City.Contains(search)
                || row.CarrierName != null && row.CarrierName.Contains(search)
                || row.ShipmentReference != null && row.ShipmentReference.Contains(search));
        }

        var total = await query.CountAsync(ct);
        var pages = (int)Math.Max(1L, (total + (long)PageSize - 1) / PageSize);
        page = Math.Min(page, pages);
        var rows = await query.OrderByDescending(row => row.UpdatedAtUtc).ThenByDescending(row => row.OrderId)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new DeliveryListPage { Rows = rows, State = canonical, Search = search, Page = page, Pages = pages, Total = total };
    }
}
