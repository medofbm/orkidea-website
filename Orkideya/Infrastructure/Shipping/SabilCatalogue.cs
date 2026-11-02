using System.Text.Json;

namespace Orkideya.Infrastructure.Shipping;

// Catalogue rates are provider reference data, never a final checkout quote.
public sealed record SabilArea(string Name, decimal? DeliveryRate, decimal? PickupRate);
public sealed record SabilBranch(string Id, string Country, string City, string Currency, IReadOnlyList<SabilArea> Areas);
public sealed record SabilPackage(string Id, string Title, string Currency, decimal Amount, bool IsPrimary);
public sealed record SabilBranchPage(IReadOnlyList<SabilBranch> Branches, int? TotalCount);
public sealed record SabilPackagePage(IReadOnlyList<SabilPackage> Packages, int? TotalCount);
public enum SabilDestinationState { NotInPage, AmbiguousInPage, SingleCandidate }
public sealed record SabilDestinationMatch(SabilDestinationState State, SabilBranch? Branch = null, SabilArea? Area = null);

public static class SabilCatalogue
{
    public static bool TryReadBranches(JsonElement data, out SabilBranchPage? page)
    {
        page = null;
        if (!Rows(data, out var rows, out var total)) return false;
        var branches = new List<SabilBranch>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (!Text(row, "_id", 128, out var id) || !ids.Add(id) ||
                !Code(row, "countryCode", out var country) || !Code(row, "currency", out var currency) ||
                !Text(row, "city", 200, out var city)) return false;
            var areas = new List<SabilArea>();
            if (row.TryGetProperty("areas", out var areaRows) && areaRows.ValueKind != JsonValueKind.Null)
            {
                if (areaRows.ValueKind != JsonValueKind.Array || areaRows.GetArrayLength() > 2000) return false;
                foreach (var area in areaRows.EnumerateArray())
                {
                    if (!Text(area, "area", 200, out var name) ||
                        !Rate(area, "deliveryRate", out var delivery) || !Rate(area, "pickupRate", out var pickup)) return false;
                    // Keep duplicates: a later resolution must report ambiguity, not silently pick a rate.
                    areas.Add(new(name, delivery, pickup));
                }
            }
            branches.Add(new(id, country, city, currency, areas.AsReadOnly()));
        }
        page = new(branches.AsReadOnly(), total);
        return true;
    }

    public static bool TryReadPackages(JsonElement data, out SabilPackagePage? page)
    {
        page = null;
        if (!Rows(data, out var rows, out var total)) return false;
        var packages = new List<SabilPackage>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (!Text(row, "_id", 128, out var id) || !ids.Add(id) || !Text(row, "title", 200, out var title) ||
                !Code(row, "currency", out var currency) || !Rate(row, "amount", out var amount) || amount is null) return false;
            var primary = false;
            if (row.TryGetProperty("isPrimary", out var primaryValue))
            {
                if (primaryValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
                primary = primaryValue.GetBoolean();
            }
            packages.Add(new(id, title, currency, amount.Value, primary));
        }
        if (packages.Count(p => p.IsPrimary) > 1) return false;
        page = new(packages.AsReadOnly(), total);
        return true;
    }

    public static SabilDestinationMatch MatchInPage(SabilBranchPage page, string country, string city, string area, string? branchId = null)
    {
        var matches = page.Branches.Where(b => b.Country == country && b.City == city && (branchId == null || b.Id == branchId))
            .SelectMany(b => b.Areas.Where(a => a.Name == area).Select(a => (Branch: b, Area: a))).Take(2).ToArray();
        return matches.Length switch
        {
            0 => new(SabilDestinationState.NotInPage),
            1 => new(SabilDestinationState.SingleCandidate, matches[0].Branch, matches[0].Area),
            _ => new(SabilDestinationState.AmbiguousInPage)
        };
    }

    private static bool Rows(JsonElement data, out JsonElement rows, out int? total)
    {
        rows = default; total = null;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("results", out rows) ||
            rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 2000 ||
            rows.EnumerateArray().Any(r => r.ValueKind != JsonValueKind.Object)) return false;
        if (data.TryGetProperty("totalCount", out var count))
        {
            if (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var n) || n < rows.GetArrayLength()) return false;
            total = n;
        }
        return true;
    }

    private static bool Text(JsonElement row, string field, int max, out string value)
    {
        value = "";
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(field, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString()!;
        // Preserve exact provider spelling; do not transliterate or trim names before matching.
        return !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
    }

    private static bool Code(JsonElement row, string field, out string value) =>
        Text(row, field, 3, out value) && value.Length == 3 && value.All(c => c is >= 'a' and <= 'z');

    private static bool Rate(JsonElement row, string field, out decimal? value)
    {
        value = null;
        if (!row.TryGetProperty(field, out var element) || element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var rate) || rate < 0) return false;
        value = rate;
        return true;
    }
}
