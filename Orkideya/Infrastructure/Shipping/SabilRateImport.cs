using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Shipping;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Infrastructure.Shipping;

public sealed record SabilRateRow(string City, string Area, decimal Price);
public sealed record SabilRatePlan(IReadOnlyList<SabilRateRow> Rows, int Cities, int Areas, int Conflicts, int SkippedNoRate);
public sealed record SabilRatePlanResult(CarrierReadState State, SabilRatePlan? Plan)
{
    public bool Succeeded => State == CarrierReadState.Success && Plan is not null;
}

/// <summary>
/// Builds a delivery-price plan from the Sabil account catalogue (home delivery rate per city and area).
/// Pure planning: it only reads from the gateway. Failures carry the sanitized read state, never provider text.
/// </summary>
public static class SabilRatePlanner
{
    public const int PageSize = 100;
    public const int MaxPages = 30;
    public const string Country = "lby";
    public const string Currency = "lyd";

    public static async Task<SabilRatePlanResult> BuildAsync(ICarrierReadGateway gateway, CancellationToken ct = default)
    {
        var prices = new Dictionary<(string City, string Area), (decimal Price, bool Conflict)>();
        var skipped = 0;
        var offset = 0;
        for (var page = 0; ; page++)
        {
            // Hitting the cap with full pages means the catalogue may be incomplete: refuse rather than apply a partial list.
            if (page >= MaxPages) return new(CarrierReadState.InvalidResponse, null);
            var read = await gateway.ListBranchesAsync(offset, PageSize, ct);
            if (read.State != CarrierReadState.Success) return new(read.State, null);
            if (read.Data is not { } data || !SabilCatalogue.TryReadBranches(data, out var parsed) || parsed is null)
                return new(CarrierReadState.InvalidResponse, null);

            foreach (var branch in parsed.Branches)
            {
                if (branch.Country != Country || branch.Currency != Currency) continue;
                var city = branch.City.Trim();
                foreach (var area in branch.Areas)
                {
                    var name = area.Name.Trim();
                    if (city.Length == 0 || name.Length == 0 || area.DeliveryRate is not { } rate) { skipped++; continue; }
                    var price = Math.Ceiling(rate * 100m) / 100m; // never round a price down
                    var key = (city, name);
                    if (!prices.TryGetValue(key, out var existing)) prices[key] = (price, false);
                    else if (existing.Price != price) prices[key] = (Math.Max(existing.Price, price), true);
                }
            }

            if (parsed.Branches.Count < PageSize) break;
            offset += parsed.Branches.Count;
        }

        var rows = prices.Select(p => new SabilRateRow(p.Key.City, p.Key.Area, p.Value.Price))
            .OrderBy(r => r.City, StringComparer.Ordinal).ThenBy(r => r.Price).ThenBy(r => r.Area, StringComparer.Ordinal).ToList();
        var plan = new SabilRatePlan(rows, rows.Select(r => r.City).Distinct(StringComparer.Ordinal).Count(), rows.Count,
            prices.Values.Count(p => p.Conflict), skipped);
        return new(CarrierReadState.Success, plan);
    }
}

public sealed class SabilRateImport(ICarrierReadGateway gateway, ApplicationDbContext db)
{
        public Task<SabilRatePlanResult> PlanAsync(CancellationToken ct = default) => SabilRatePlanner.BuildAsync(gateway, ct);

    /// <summary>Replaces every ShippingRates row with the plan in one transaction. An empty plan is refused.</summary>
    public async Task<bool> ApplyAsync(SabilRatePlan plan, CancellationToken ct = default)
    {
        if (plan.Rows.Count == 0) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        db.ShippingRates.RemoveRange(await db.ShippingRates.ToListAsync(ct));
        db.ShippingRates.AddRange(plan.Rows.Select(row => new ShippingRate
        {
            CityName = row.City + DeliveryRateGroups.AreaSeparator + row.Area,
            Region = row.City,
            Price = row.Price,
            DeliveryDuration = ""
        }));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}

public sealed record DeliveryArea(string Area, decimal Price);
public sealed record DeliveryGroup(string City, decimal MinPrice, decimal MaxPrice, IReadOnlyList<DeliveryArea> Areas);

/// <summary>Pure grouping of stored shipping rates by Region for customer display.</summary>
public static class DeliveryRateGroups
{
    public const string AreaSeparator = " — ";

    /// <summary>The area label of a stored rate: CityName without its leading "Region — " when present.</summary>
    public static string AreaLabel(string region, string cityName)
    {
        var prefix = region + AreaSeparator;
        return region.Length > 0 && cityName.StartsWith(prefix, StringComparison.Ordinal) && cityName.Length > prefix.Length
            ? cityName[prefix.Length..] : cityName;
    }

    public static IReadOnlyList<DeliveryGroup> Build(IEnumerable<(string Region, string CityName, decimal Price)> rates) =>
        rates.GroupBy(r => r.Region, StringComparer.Ordinal)
            .Select(g =>
            {
                var areas = g.Select(r => new DeliveryArea(AreaLabel(r.Region, r.CityName), r.Price))
                    .OrderBy(a => a.Price).ThenBy(a => a.Area, StringComparer.Ordinal).ToList();
                return new DeliveryGroup(g.Key, areas.Min(a => a.Price), areas.Max(a => a.Price), areas);
            })
            .OrderBy(g => g.MinPrice).ThenBy(g => g.City, StringComparer.Ordinal).ToList();
}
