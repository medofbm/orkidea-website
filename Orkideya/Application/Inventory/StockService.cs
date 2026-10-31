using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Inventory;

public enum StockResult { Changed, Unchanged, Invalid, Missing, Conflict, Insufficient }

public sealed class StockService(ApplicationDbContext db, TimeProvider clock)
{
    public const decimal Maximum = 1_000_000_000m;
    public static bool ValidActor(string actor) => !string.IsNullOrWhiteSpace(actor) && actor.Length <= 450;
    public static bool ValidAmount(decimal amount, string unit) => decimal.Round(amount, 3) == amount &&
        Math.Abs(amount) <= Maximum && (unit == "ml" || unit == "piece" && decimal.Truncate(amount) == amount);

    private StockMovement Movement(int account, int? order, string kind, decimal onHand, decimal reserved, string actor, string reason, Guid? request = null) => new()
    {
        StockMovementId = request ?? Guid.NewGuid(), StockAccountId = account, OrderId = order,
        Kind = kind, OnHandDelta = onHand, ReservedDelta = reserved, ActorUserId = actor,
        Reason = reason, CreatedAtUtc = clock.GetUtcNow().UtcDateTime
    };

    private async Task<StockResult> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return StockResult.Changed; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return StockResult.Conflict; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { db.ChangeTracker.Clear(); return StockResult.Conflict; }
    }

    public async Task<StockResult> OpenAsync(int productId, string unit, string actor, CancellationToken ct = default)
    {
        if (!ValidActor(actor) || unit is not ("ml" or "piece")) return StockResult.Invalid;
        if (!await db.Products.AnyAsync(p => p.ProductId == productId, ct)) return StockResult.Missing;
        if (await db.StockAccounts.AnyAsync(s => s.ProductId == productId, ct)) return StockResult.Conflict;
        db.StockAccounts.Add(new StockAccount { ProductId = productId, Unit = unit });
        db.StockMovements.Add(Movement(productId, null, "Setup", 0, 0, actor, "تفعيل التتبع برصيد صفر؛ لم يفترض رصيد افتتاحي."));
        return await SaveAsync(ct);
    }

    public async Task<StockResult> ConfigureAsync(int productId, int variantId, decimal units, int revision, string actor, CancellationToken ct = default)
    {
        if (!ValidActor(actor) || units <= 0 || units > 1_000_000m) return StockResult.Invalid;
        var stock = await db.StockAccounts.SingleOrDefaultAsync(s => s.ProductId == productId, ct);
        var variant = await db.ProductVariants.SingleOrDefaultAsync(v => v.ProductVariantId == variantId && v.ProductId == productId, ct);
        if (stock is null || variant is null) return StockResult.Missing;
        if (stock.Revision != revision || stock.Revision == int.MaxValue) return StockResult.Conflict;
        if (!ValidAmount(units, stock.Unit)) return StockResult.Invalid;
        if (variant.StockUnitsPerItem == units) return StockResult.Unchanged;
        variant.StockUnitsPerItem = units; stock.Revision++;
        db.StockMovements.Add(Movement(productId, null, "Configure", 0, 0, actor,
            $"استهلاك الحجم #{variantId}: {units.ToString(System.Globalization.CultureInfo.InvariantCulture)} {stock.Unit}"));
        return await SaveAsync(ct);
    }

    public async Task<StockResult> AdjustAsync(int productId, decimal delta, int revision, Guid request, string actor, string reason, CancellationToken ct = default)
    {
        if (!ValidActor(actor) || request == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || delta == 0)
            return StockResult.Invalid;
        var prior = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.StockMovementId == request, ct);
        if (prior is not null)
            return prior.StockAccountId == productId && prior.Kind == "Adjust" && prior.OnHandDelta == delta && prior.Reason == reason.Trim()
                ? StockResult.Unchanged : StockResult.Conflict;
        var stock = await db.StockAccounts.SingleOrDefaultAsync(s => s.ProductId == productId, ct);
        if (stock is null) return StockResult.Missing;
        if (stock.Revision != revision || stock.Revision == int.MaxValue) return StockResult.Conflict;
        if (!ValidAmount(delta, stock.Unit)) return StockResult.Invalid;
        var next = stock.OnHand + delta;
        if (next < stock.Reserved || next < 0 || next > Maximum) return StockResult.Insufficient;
        stock.OnHand = next; stock.Revision++;
        db.StockMovements.Add(Movement(productId, null, "Adjust", delta, 0, actor, reason.Trim(), request));
        return await SaveAsync(ct);
    }

    public async Task<StockResult> ApplyOrderAsync(int id, int revision, string operation, string actor, CancellationToken ct = default)
    {
        if (!ValidActor(actor) || operation is not ("Reserve" or "Consume")) return StockResult.Invalid;
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.OrderId == id, ct);
        if (order is null) return StockResult.Missing;
        if (order.StatusRevision != revision || order.StatusRevision == int.MaxValue) return StockResult.Conflict;
        if (order.Status is not ("Pending" or "Processing")) return StockResult.Invalid;
        var target = operation == "Reserve" ? "Reserved" : "Filled";
        if (order.StockState == target) return StockResult.Unchanged;
        if (order.StockState != (operation == "Reserve" ? "AwaitingConfirmation" : "Reserved")) return StockResult.Invalid;
        var result = await StageOrderAsync(order, operation, actor, ct);
        if (result != StockResult.Changed) return result;
        order.StockState = target; order.StatusRevision++;
        if (operation == "Reserve" && order.Status == "Pending")
        {
            order.Status = "Processing";
            db.OrderStatusChanges.Add(new OrderStatusChange { OrderId = id, Revision = order.StatusRevision,
                PreviousStatus = "Pending", NewStatus = "Processing", ActorUserId = actor,
                Reason = "تأكيد التوفر وحجز الكمية", ChangedAtUtc = clock.GetUtcNow().UtcDateTime });
        }
        return await SaveAsync(ct);
    }

    // Stage only: cancellation saves the order status, history and release in the same transaction.
    public async Task<StockResult> StageCancellationAsync(Order order, string actor, string reason, CancellationToken ct)
    {
        if (order.StockState == "Reserved")
        {
            await db.Entry(order).Collection(o => o.Items).LoadAsync(ct);
            var result = await StageOrderAsync(order, "Release", actor, ct, reason);
            if (result != StockResult.Changed) return result;
            order.StockState = "Cancelled";
        }
        else if (order.StockState == "AwaitingConfirmation") order.StockState = "Cancelled";
        // Filled oil stays consumed: cancelling never pours it back into bulk stock.
        return StockResult.Changed;
    }

    private async Task<StockResult> StageOrderAsync(Order order, string kind, string actor, CancellationToken ct, string? reason = null)
    {
        if (order.Items.Count == 0 || order.Items.Any(i => i.StockAccountId != i.ProductId || i.StockAccountId is null ||
            i.StockUnitsPerItem is null or <= 0 || i.Quantity <= 0)) return StockResult.Invalid;
        var requirements = order.Items.GroupBy(i => i.StockAccountId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.StockUnitsPerItem!.Value * i.Quantity));
        var ids = requirements.Keys.ToArray();
        var accounts = await db.StockAccounts.Where(s => ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId, ct);
        if (accounts.Count != requirements.Count) return StockResult.Invalid;
        foreach (var (id, amount) in requirements)
        {
            var account = accounts[id];
            if (account.Revision == int.MaxValue || order.Items.Any(i => i.StockAccountId == id && i.StockUnit != account.Unit)) return StockResult.Invalid;
            if (kind == "Reserve" && account.OnHand - account.Reserved < amount ||
                kind != "Reserve" && (account.Reserved < amount || kind == "Consume" && account.OnHand < amount)) return StockResult.Insufficient;
        }
        foreach (var (id, amount) in requirements)
        {
            var account = accounts[id];
            var reservedDelta = kind == "Reserve" ? amount : -amount;
            var onHandDelta = kind == "Consume" ? -amount : 0;
            account.Reserved += reservedDelta; account.OnHand += onHandDelta; account.Revision++;
            db.StockMovements.Add(Movement(id, order.OrderId, kind, onHandDelta, reservedDelta, actor,
                reason ?? (kind == "Reserve" ? "تأكيد المدير وحجز الكمية" : "تسجيل التعبئة وصرف الكمية المحجوزة")));
        }
        return StockResult.Changed;
    }
}
