using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Delivery;

public enum DeliveryResult { Changed, Unchanged, Invalid, Missing, Conflict, RequiresPreparation }

public sealed class DeliveryService(ApplicationDbContext db, TimeProvider clock)
{
    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static bool ActorValid(string actor) => !string.IsNullOrWhiteSpace(actor) && actor.Length <= 450;
    private static string OrderState(string deliveryState) => deliveryState switch
    {
        "HandedOver" or "Returning" => "Shipped", "Delivered" => "Delivered", "Returned" => "Cancelled",
        _ => ""
    };

    public async Task<DeliveryResult> ApplyAsync(int orderId, DeliveryInput input, string actor, CancellationToken ct = default)
    {
        var action = input.Action;
        var reason = Clean(input.Reason);
        var carrier = Clean(input.CarrierName);
        var reference = Clean(input.ShipmentReference);
        var mode = Clean(input.Mode);
        var target = Clean(input.TargetState);
        var metadata = action is "HandOver" or "Redispatch" or "CorrectDetails";
        if (orderId <= 0 || !ActorValid(actor) || input.ExpectedRevision is null or < 0 or int.MaxValue ||
            input.RequestId is null || input.RequestId == Guid.Empty || reason?.Length > 500 ||
            carrier?.Length > 120 || reference?.Length > 100 ||
            action is not ("HandOver" or "Deliver" or "MarkReturning" or "ReceiveReturn" or "Resume" or "Redispatch" or "CorrectDetails" or "CorrectState"))
            return DeliveryResult.Invalid;
        if (metadata)
        {
            if (mode is not ("Company" or "Self") || mode == "Company" && carrier is null) return DeliveryResult.Invalid;
            if (mode == "Self") carrier = null;
        }
        else if (mode is not null || carrier is not null || reference is not null) return DeliveryResult.Invalid;
        if ((action is "MarkReturning" or "Resume" or "Redispatch" or "CorrectDetails" or "CorrectState") && reason is null)
            return DeliveryResult.Invalid;
        if (action != "HandOver" && input.ConfirmLegacyPrepared ||
            action != "ReceiveReturn" && input.ConfirmReturnReceived ||
            action != "Redispatch" && input.ConfirmSamePackageReusable) return DeliveryResult.Invalid;
        if (action == "ReceiveReturn" && !input.ConfirmReturnReceived ||
            action == "Redispatch" && !input.ConfirmSamePackageReusable ||
            action != "CorrectState" && target is not null ||
            action == "CorrectState" && target is not ("HandedOver" or "Returning")) return DeliveryResult.Invalid;

        // Bind replay to the full normalized request, expected version, order and authenticated actor.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            orderId, action, mode, carrier, reference, reason, target, input.ExpectedRevision, actor,
            input.ConfirmLegacyPrepared, input.ConfirmReturnReceived, input.ConfirmSamePackageReusable
        }))));
        var replay = await db.OrderDeliveryEvents.AsNoTracking().SingleOrDefaultAsync(e => e.RequestId == input.RequestId, ct);
        if (replay is not null) return replay.RequestHash == hash ? DeliveryResult.Unchanged : DeliveryResult.Conflict;
        var order = await db.Orders.SingleOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return DeliveryResult.Missing;
        if (order.StatusRevision != input.ExpectedRevision) return await ResolveReplay(input.RequestId.Value, hash, ct);
        var delivery = await db.OrderDeliveries.SingleOrDefaultAsync(d => d.OrderId == orderId, ct);
        // The order may have been read just before a competing commit and its shipment just after it.
        // Resolve a matching committed key before treating those cross-query reads as a conflict.
        if (delivery is not null && order.Status != OrderState(delivery.State))
            return await ResolveReplay(input.RequestId.Value, hash, ct);
        var previousState = delivery?.State;
        string next;
        switch (action)
        {
            case "HandOver":
                if (delivery is not null) return await ResolveReplay(input.RequestId.Value, hash, ct);
                if (order.Status != "Processing" || order.StockState is not ("Untracked" or "Filled") ||
                    !await db.OrderItems.AnyAsync(i => i.OrderId == orderId, ct)) return DeliveryResult.RequiresPreparation;
                if (order.StockState == "Untracked" && !input.ConfirmLegacyPrepared) return DeliveryResult.RequiresPreparation;
                if (order.StockState != "Untracked" && input.ConfirmLegacyPrepared) return DeliveryResult.Invalid;
                next = "HandedOver";
                break;
            case "Deliver" when previousState == "HandedOver": next = "Delivered"; break;
            case "MarkReturning" when previousState == "HandedOver": next = "Returning"; break;
            case "ReceiveReturn" when previousState == "Returning": next = "Returned"; break;
            case "Resume" when previousState == "Returning": next = "HandedOver"; break;
            case "Redispatch" when previousState == "Returned": next = "HandedOver"; break;
            case "CorrectDetails" when previousState is not null: next = previousState; break;
            case "CorrectState" when previousState == "Delivered" && target == "HandedOver": next = target; break;
            case "CorrectState" when previousState == "Returned" && target == "Returning": next = target; break;
            default: return DeliveryResult.Invalid;
        }
        // Already filled packages never consume bulk stock again, including return/redispatch/corrections.
        if (order.StockState is not ("Untracked" or "Filled")) return DeliveryResult.RequiresPreparation;
        var now = clock.GetUtcNow().UtcDateTime;
        var oldStatus = order.Status;
        order.Status = OrderState(next);
        order.StatusRevision++;
        if (delivery is null)
        {
            delivery = new OrderDelivery { OrderId = orderId, Mode = mode!, State = next, CreatedAtUtc = now };
            db.OrderDeliveries.Add(delivery);
        }
        if (metadata) { delivery.Mode = mode!; delivery.CarrierName = carrier; delivery.ShipmentReference = reference; }
        delivery.State = next;
        delivery.Revision = order.StatusRevision;
        delivery.UpdatedAtUtc = now;
        db.OrderDeliveryEvents.Add(new OrderDeliveryEvent
        {
            RequestId = input.RequestId.Value, OrderId = orderId, Revision = order.StatusRevision,
            Action = action!, PreviousState = previousState, NewState = next, Mode = delivery.Mode,
            CarrierName = delivery.CarrierName, ShipmentReference = delivery.ShipmentReference,
            Reason = reason, ActorUserId = actor, RequestHash = hash, ChangedAtUtc = now,
            LegacyPreparationConfirmed = input.ConfirmLegacyPrepared, ReturnReceiptConfirmed = input.ConfirmReturnReceived,
            SamePackageReuseConfirmed = input.ConfirmSamePackageReusable
        });
        if (oldStatus != order.Status)
            db.OrderStatusChanges.Add(new OrderStatusChange
            {
                OrderId = orderId, Revision = order.StatusRevision, PreviousStatus = oldStatus, NewStatus = order.Status,
                ActorUserId = actor, ChangedAtUtc = now,
                Reason = reason ?? DeliveryLabels.Action(action!)
            });
        try
        {
            // One EF transaction commits order CAS, current delivery and immutable event together.
            await db.SaveChangesAsync(ct);
            return DeliveryResult.Changed;
        }
        catch (DbUpdateConcurrencyException) { return await ResolveReplay(input.RequestId.Value, hash, ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return await ResolveReplay(input.RequestId.Value, hash, ct); }
    }

    private async Task<DeliveryResult> ResolveReplay(Guid id, string hash, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var saved = await db.OrderDeliveryEvents.AsNoTracking().SingleOrDefaultAsync(e => e.RequestId == id, ct);
        return saved?.RequestHash == hash ? DeliveryResult.Unchanged : DeliveryResult.Conflict;
    }
}
