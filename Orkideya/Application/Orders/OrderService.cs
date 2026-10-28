using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orkideya.Application.Orders;

public sealed record PlacedOrder(Order Order, bool IsNew);
public sealed class CheckoutConflictException : Exception { }

public sealed class OrderService(ApplicationDbContext context)
{
    public static string RequestHash(CheckoutInput input) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]
        {
            input.FirstName.Trim(), input.LastName.Trim(), input.WhatsAppNumber.Trim(),
            input.ShippingAddress.Trim(), input.ShippingRateId.ToString(CultureInfo.InvariantCulture)
        }))));

    public async Task<Order?> FindAsync(Guid token, string requestHash, CancellationToken cancellationToken = default)
    {
        var existing = await context.Orders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.CheckoutToken == token, cancellationToken);
        if (existing != null && existing.CheckoutRequestHash != requestHash) throw new CheckoutConflictException();
        return existing;
    }

    public async Task<PlacedOrder> PlaceAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.CheckoutToken is null || order.CheckoutToken == Guid.Empty ||
            order.CheckoutRequestHash?.Length != 64 || order.Items.Count == 0 || order.ShippingCost < 0 ||
            order.Items.Any(item => item.Quantity is < 1 or > 99 || item.UnitPrice <= 0 ||
                decimal.Round(item.UnitPrice, 2) != item.UnitPrice) ||
            order.TotalAmount != order.Items.Sum(item => item.UnitPrice * item.Quantity) + order.ShippingCost)
            throw new ArgumentException("Order aggregate is invalid.", nameof(order));

        var existing = await FindAsync(order.CheckoutToken.Value, order.CheckoutRequestHash, cancellationToken);
        if (existing != null) return new(existing, false);
        context.Orders.Add(order);
        try
        {
            // One aggregate and one SaveChanges: EF's transaction includes the order and every item.
            await context.SaveChangesAsync(cancellationToken);
            return new(order, true);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request may have committed this token first. Do not replay its notification.
            foreach (var item in order.Items.ToArray()) context.Entry(item).State = EntityState.Detached;
            context.Entry(order).State = EntityState.Detached;
            existing = await FindAsync(order.CheckoutToken.Value, order.CheckoutRequestHash, cancellationToken);
            if (existing == null) throw;
            return new(existing, false);
        }
    }
}
