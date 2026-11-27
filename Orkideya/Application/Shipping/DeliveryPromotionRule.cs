using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Shipping;

/// <summary>Result of applying the delivery promotion to one quoted/stored fee.</summary>
/// <param name="Fee">The fee the customer is charged.</param>
/// <param name="OriginalFee">The normal fee when the promotion made it free; null when no discount applied.</param>
public sealed record PromotedFee(decimal Fee, decimal? OriginalFee)
{
    public bool IsFree => OriginalFee is not null;
}

/// <summary>
/// The single place that decides free delivery for website orders. Pure and unit-tested.
/// Manual (Facebook) orders never call this.
/// </summary>
public static class DeliveryPromotionRule
{
    public const string TripoliCity = "طرابلس";

    /// <summary>Whether the promotion is on at this moment: enabled and now within [Starts, Ends).</summary>
    public static bool IsActive(DeliveryPromotion? promotion, DateTime nowUtc) =>
        promotion is { IsEnabled: true } p && p.StartsAtUtc <= nowUtc && nowUtc < p.EndsAtUtc &&
        p.Scope is DeliveryPromotion.ScopeTripoli or DeliveryPromotion.ScopeAllLibya;

    /// <summary>
    /// True for the city «طرابلس» itself and its areas. Imported carrier rates are stored as
    /// Region = city and CityName = "city — area"; Sabil destinations carry the city on its own.
    /// «ضواحي طرابلس» or other cities never match.
    /// </summary>
    public static bool IsTripoli(string? city, string? region = null)
    {
        static bool Same(string? value) => value?.Trim() == TripoliCity;
        if (Same(city) || Same(region)) return true;
        var name = city?.Trim() ?? "";
        return name.StartsWith(TripoliCity + " — ", StringComparison.Ordinal);
    }

    public static bool Covers(DeliveryPromotion promotion, string? city, string? region) =>
        promotion.Scope == DeliveryPromotion.ScopeAllLibya ||
        (promotion.Scope == DeliveryPromotion.ScopeTripoli && IsTripoli(city, region));

    /// <summary>Whether the cart subtotal reaches the promotion minimum (no minimum = always).</summary>
    public static bool MeetsMinimum(DeliveryPromotion promotion, decimal subtotal) =>
        promotion.MinSubtotal is not { } min || subtotal >= min;

    /// <summary>
    /// Fee to charge. Free (0) only when the promotion is active, the destination is in scope and the
    /// subtotal reaches the minimum. A fee that is already 0 (for example office pickup) stays 0 with no discount recorded.
    /// </summary>
    public static PromotedFee Apply(DeliveryPromotion? promotion, DateTime nowUtc, decimal baseFee,
        string? city, string? region, decimal subtotal)
    {
        if (baseFee <= 0 || !IsActive(promotion, nowUtc) || !Covers(promotion!, city, region) ||
            !MeetsMinimum(promotion!, subtotal))
            return new(baseFee, null);
        return new(0m, baseFee);
    }
}

/// <summary>Loads the current promotion once per request (scoped).</summary>
public sealed class DeliveryPromotionService(ApplicationDbContext context)
{
    private DeliveryPromotion? cached;
    private bool loaded;

    /// <summary>The configured promotion row (active or not), or null when none exists.</summary>
    public async Task<DeliveryPromotion?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (loaded) return cached;
        cached = await context.DeliveryPromotions.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
        loaded = true;
        return cached;
    }

    /// <summary>The promotion when it is active right now, else null.</summary>
    public async Task<DeliveryPromotion?> GetActiveAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var promotion = await GetAsync(cancellationToken);
        return DeliveryPromotionRule.IsActive(promotion, nowUtc) ? promotion : null;
    }
}
