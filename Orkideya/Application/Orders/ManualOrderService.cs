using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Orders;

public sealed class ManualOrderService(ApplicationDbContext db, OrderService orders)
{
    public static string Hash(ManualOrderDraft draft) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        draft.RequestId,
        draft.ActorId,
        draft.Input.CustomerName,
        draft.Input.Phone,
        draft.Input.Address,
        draft.Input.ShippingRateId,
        draft.City,
        draft.ShippingCost,
        draft.Lines
    }))));
    private static bool ValidActor(string actor) => !string.IsNullOrWhiteSpace(actor) && actor.Length <= 450;

    public async Task<ManualPreview> PreviewAsync(ManualOrderInput input, string actor, CancellationToken ct = default)
    {
        if (!ValidActor(actor) || input.RequestId is null || input.RequestId == Guid.Empty ||
            !Validator.TryValidateObject(input, new ValidationContext(input), [], true) || input.Lines is null || input.Lines.Count is < 1 or > 30 ||
            input.Lines.Any(l => l is null || !Validator.TryValidateObject(l, new ValidationContext(l), [], true)))
            return new(null, "راجع اسم الزبون والهاتف والعنوان والمدينة والبنود والكميات.");
        var grouped = input.Lines.GroupBy(l => l.VariantId).Select(g => new ManualOrderLineInput { VariantId = g.Key, Quantity = g.Sum(l => l.Quantity) })
            .OrderBy(l => l.VariantId).ToList();
        if (grouped.Any(l => l.Quantity > 99)) return new(null, "مجموع كمية الحجم نفسه لا يتجاوز 99؛راجع الصفوف المتكررة.");
        var ids = grouped.Select(l => l.VariantId).ToArray();
        var variants = await db.ProductVariants.AsNoTracking().Include(v => v.Product).Where(v => ids.Contains(v.ProductVariantId)).ToDictionaryAsync(v => v.ProductVariantId, ct);
        if (variants.Count != ids.Length || variants.Values.Any(v => v.Product is null || string.IsNullOrWhiteSpace(v.Size) || v.Price <= 0 || decimal.Round(v.Price, 2) != v.Price))
            return new(null, "أحد الأحجام غير متاح أو سعره غير صالح؛راجع اختيار المنتجات.");
        var rate = await db.ShippingRates.AsNoTracking().SingleOrDefaultAsync(r => r.ShippingRateId == input.ShippingRateId, ct);
        if (rate is null || rate.Price < 0 || string.IsNullOrWhiteSpace(rate.CityName)) return new(null, "مدينة التوصيل غير متاحة؛اختر مدينة من القائمة.");
        var products = variants.Values.Select(v => v.ProductId).Distinct().ToArray();
        var stock = await db.StockAccounts.AsNoTracking().Where(s => products.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId, ct);
        if (stock.Count > 0 && variants.Values.Any(v => !stock.ContainsKey(v.ProductId)) ||
            variants.Values.Any(v => stock.TryGetValue(v.ProductId, out var a) && (v.StockUnitsPerItem is null or <= 0 or > 1000000 ||
                decimal.Round(v.StockUnitsPerItem.Value, 3) != v.StockUnitsPerItem || a.Unit is not ("ml" or "piece") ||
                a.Unit == "piece" && decimal.Truncate(v.StockUnitsPerItem.Value) != v.StockUnitsPerItem)))
            return new(null, "أكمل إعداد مخزون كل أحجام الطلب؛لا نحفظ طلباً مختلطاً أو استهلاكاً مفترضاً.");
        var lines = grouped.Select(l =>
        {
            var v = variants[l.VariantId]; var account = stock.GetValueOrDefault(v.ProductId);
            return new ManualDraftLine(v.ProductVariantId, v.ProductId, v.Product.Name, v.Size, l.Quantity, v.Price,
                account?.ProductId, account is null ? null : v.StockUnitsPerItem, account?.Unit);
        }).ToList();
        if (lines.Sum(l => l.Quantity * l.UnitPrice) + rate.Price > 9999999999999999.99m) return new(null, "الإجمالي يتجاوز الحد المسموح.");
        var normalized = new ManualOrderInput
        {
            RequestId = input.RequestId,
            CustomerName = input.CustomerName.Trim(),
            Phone = input.Phone.Trim(),
            Address = input.Address.Trim(),
            ShippingRateId = input.ShippingRateId,
            Lines = grouped
        };
        var draft = new ManualOrderDraft(input.RequestId.Value, actor, normalized, rate.CityName, rate.Price, lines);
        return new(draft with { Fingerprint = Hash(draft) }, null);
    }

    public async Task<Order?> FindOwnAsync(Guid token, string actor, CancellationToken ct = default)
    {
        if (token == Guid.Empty || !ValidActor(actor)) return null;
        var existing = await db.Orders.AsNoTracking().Include(o => o.Creation).SingleOrDefaultAsync(o => o.CheckoutToken == token, ct);
        if (existing is not null && (existing.Creation?.Source != OrderSources.Facebook || existing.Creation.ActorUserId != actor))
            throw new CheckoutConflictException();
        return existing;
    }

    public Task<ManualSaveResult> SaveAsync(ManualOrderDraft draft, string actor, CancellationToken ct = default)
    {
        if (!ValidActor(actor) || draft.ActorId != actor || draft.RequestId == Guid.Empty || draft.Input.RequestId != draft.RequestId || draft.Fingerprint != Hash(draft))
            return Task.FromResult(new ManualSaveResult(ManualSaveState.Invalid, Error: "انتهت صلاحية المعاينة؛راجع الطلب من جديد."));
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // This request owns the context. A transient retry must not reuse a partly tracked graph.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var existing = await orders.FindAsync(draft.RequestId, draft.Fingerprint, ct);
            if (existing is not null)
            {
                if (existing.Creation?.Source != OrderSources.Facebook || existing.Creation.ActorUserId != actor) throw new CheckoutConflictException();
                await transaction.CommitAsync(ct);
                return new ManualSaveResult(ManualSaveState.Existing, existing.OrderId);
            }
            // Hold the reviewed catalogue/rate/configuration stable through the atomic order commit.
            var latest = await PreviewAsync(draft.Input, actor, ct);
            if (latest.Draft is null) return new ManualSaveResult(ManualSaveState.Invalid, Error: latest.Error);
            if (latest.Draft.Fingerprint != draft.Fingerprint)
                return new ManualSaveResult(ManualSaveState.ReviewChanged, Draft: latest.Draft,
                    Error: "تغير السعر أو التوصيل أو إعداد أحد الأحجام. راجع المعاينة الجديدة ثم احفظ صراحة.");
            var order = new Order
            {
                CustomerName = draft.Input.CustomerName,
                WhatsAppNumber = draft.Input.Phone,
                ShippingAddress = draft.Input.Address,
                City = draft.City,
                ShippingCost = draft.ShippingCost,
                TotalAmount = draft.Total,
                OrderDate = DateTime.UtcNow,
                PaymentMethod = "الدفع عند الاستلام",
                CheckoutToken = draft.RequestId,
                CheckoutRequestHash = draft.Fingerprint,
                Creation = new OrderCreation { Source = OrderSources.Facebook, ActorUserId = actor },
                Items = draft.Lines.Select(l => new OrderItem
                {
                    ProductId = l.ProductId,
                    VariantSize = l.Size,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    StockAccountId = l.StockAccountId,
                    StockUnitsPerItem = l.StockUnitsPerItem,
                    StockUnit = l.StockUnit
                }).ToList()
            };
            var saved = await orders.PlaceAsync(order, ct);
            await transaction.CommitAsync(ct);
            return new ManualSaveResult(saved.IsNew ? ManualSaveState.Created : ManualSaveState.Existing, saved.Order.OrderId);
        });
    }
}
