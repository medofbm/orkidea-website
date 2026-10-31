using Orkideya.Application.Inventory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Orders;

public enum StatusChangeResult { Changed, Unchanged, Missing, Invalid, Conflict, RequiresFilling, RequiresCancellationReason, ClosedStockOrder, RequiresDeliveryOperation }

public sealed class OrderStatusService(ApplicationDbContext context, TimeProvider clock)
{
    private static readonly string[] Statuses = ["Pending", "Processing", "Shipped", "Delivered", "Cancelled"];

    public async Task<StatusChangeResult> ChangeAsync(int id, string? status, int? expectedRevision,
        string actorUserId, CancellationToken cancellationToken = default, string? reason = null)
    {
        var canonical = Statuses.FirstOrDefault(value => value.Equals(status, StringComparison.OrdinalIgnoreCase));
        if (canonical is null || expectedRevision is null or < 0 ||
            string.IsNullOrWhiteSpace(actorUserId) || actorUserId.Length > 450)
            return StatusChangeResult.Invalid;

        var order = await context.Orders.SingleOrDefaultAsync(order => order.OrderId == id, cancellationToken);
        if (order is null) return StatusChangeResult.Missing;
        if (order.StatusRevision != expectedRevision) return StatusChangeResult.Conflict;
        if (order.Status == canonical) return StatusChangeResult.Unchanged;
        // Unknown historical states require reconciliation, not a fabricated history entry.
        if (!Statuses.Contains(order.Status) || order.StatusRevision == int.MaxValue)
            return StatusChangeResult.Invalid;

        if (await context.OrderDeliveries.AnyAsync(d => d.OrderId == id, cancellationToken))
            return StatusChangeResult.RequiresDeliveryOperation;

        if (order.StockState != "Untracked")
        {
            if (order.Status == "Cancelled" || order.StockState == "Cancelled") return StatusChangeResult.ClosedStockOrder;
            if (canonical is "Shipped" or "Delivered" && order.StockState != "Filled") return StatusChangeResult.RequiresFilling;
            if (canonical is "Shipped" or "Delivered") return StatusChangeResult.RequiresDeliveryOperation;
            if (canonical == "Cancelled")
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return StatusChangeResult.RequiresCancellationReason;
                var release = await new StockService(context, clock).StageCancellationAsync(order, actorUserId, reason.Trim(), cancellationToken);
                if (release != StockResult.Changed) return StatusChangeResult.Invalid;
            }
        }
        var previous = order.Status;
        order.Status = canonical;
        order.StatusRevision++;
        context.OrderStatusChanges.Add(new OrderStatusChange
        {
            OrderId = id, Revision = order.StatusRevision, PreviousStatus = previous,
            NewStatus = canonical, ActorUserId = actorUserId, Reason = reason?.Trim(), ChangedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        try
        {
            // EF commits the status and its history in the same transaction.
            await context.SaveChangesAsync(cancellationToken);
            return StatusChangeResult.Changed;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            context.ChangeTracker.Clear();
            return StatusChangeResult.Conflict;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return StatusChangeResult.Conflict;
        }
    }
}
