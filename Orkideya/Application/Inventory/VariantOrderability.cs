using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Inventory;

/// <summary>
/// The single server-side definition of "a size can be ordered". A product without a stock account is always
/// orderable; once the owner opens inventory for a product, each size must say how much stock it consumes.
/// </summary>
public static class VariantOrderability
{
    public const string UnavailableMessage = "هذا الحجم غير متاح للطلب حالياً.";

    public static bool IsOrderable(bool productHasStockAccount, decimal? stockUnitsPerItem) =>
        !productHasStockAccount || stockUnitsPerItem is > 0;

    /// <summary>Product ids (among <paramref name="productIds"/>) that have a stock account.</summary>
    public static async Task<HashSet<int>> TrackedProductsAsync(ApplicationDbContext db, IReadOnlyCollection<int> productIds, CancellationToken ct = default) =>
        (await db.StockAccounts.AsNoTracking().Where(s => productIds.Contains(s.ProductId)).Select(s => s.ProductId).ToListAsync(ct)).ToHashSet();

    public static bool IsOrderable(ProductVariant variant, ISet<int> trackedProducts) =>
        IsOrderable(trackedProducts.Contains(variant.ProductId), variant.StockUnitsPerItem);

    /// <summary>Active sizes of active products with a stock account but no consumption amount.</summary>
    public static async Task<List<UnorderableSize>> UnorderableSizesAsync(ApplicationDbContext db, CancellationToken ct = default) =>
        await (from variant in db.ProductVariants.AsNoTracking()
               join account in db.StockAccounts.AsNoTracking() on variant.ProductId equals account.ProductId
               where !variant.IsArchived && !variant.Product.IsArchived && !(variant.StockUnitsPerItem > 0)
               orderby variant.ProductId, variant.ProductVariantId
               select new UnorderableSize(variant.ProductId, variant.Product.Name, variant.Size)).ToListAsync(ct);
}

public sealed record UnorderableSize(int ProductId, string Name, string Size);
