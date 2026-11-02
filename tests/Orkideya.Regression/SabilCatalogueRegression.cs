using System.Text.Json;
using Orkideya.Infrastructure.Shipping;

internal static class SabilCatalogueRegression
{
    public static void Run(Action<bool, string> check)
    {
        const string branch = "{\"_id\":\"branch-a\",\"countryCode\":\"lby\",\"city\":\"Synthetic city\",\"currency\":\"lyd\",\"areas\":[{\"area\":\"Synthetic area\",\"deliveryRate\":0}]}";
        JsonElement Parse(string json) { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
        bool Branches(string json) => SabilCatalogue.TryReadBranches(Parse(json), out _);
        bool Packages(string json) => SabilCatalogue.TryReadPackages(Parse(json), out _);
        var json = "{\"results\":[" + branch + "]}";
        check(SabilCatalogue.TryReadBranches(Parse(json), out var page) && page!.TotalCount is null,
            "Typed branch page preserves unknown total count instead of assuming a complete catalogue");
        var match = SabilCatalogue.MatchInPage(page!, "lby", "Synthetic city", "Synthetic area");
        check(match.State == SabilDestinationState.SingleCandidate && match.Area!.DeliveryRate == 0 && match.Area.PickupRate is null,
            "Catalogue preserves explicit zero delivery rate and absent pickup rate as different values");
        check(SabilCatalogue.MatchInPage(page!, "lby", "Synthetic city", "Other area").State == SabilDestinationState.NotInPage,
            "Catalogue never substitutes a nearby area or the first available rate");
        check(SabilCatalogue.MatchInPage(page!, "lby", "Synthetic city ", "Synthetic area").State == SabilDestinationState.NotInPage,
            "Destination matching preserves exact provider city spelling");
        var duplicatedArea = branch.Replace("\"deliveryRate\":0}", "\"deliveryRate\":0},{\"area\":\"Synthetic area\",\"deliveryRate\":20}");
        check(SabilCatalogue.TryReadBranches(Parse("{\"results\":[" + duplicatedArea + "]}"), out var sameBranch) &&
            SabilCatalogue.MatchInPage(sameBranch!, "lby", "Synthetic city", "Synthetic area").State == SabilDestinationState.AmbiguousInPage,
            "Duplicate area names in one branch remain ambiguous instead of silently dropping a rate");
        var otherBranch = branch.Replace("branch-a", "branch-b");
        check(SabilCatalogue.TryReadBranches(Parse("{\"results\":[" + branch + "," + otherBranch + "],\"totalCount\":38}"), out var multiple) &&
            SabilCatalogue.MatchInPage(multiple!, "lby", "Synthetic city", "Synthetic area").State == SabilDestinationState.AmbiguousInPage,
            "Same city and area across branches requires explicit branch disambiguation");
        check(SabilCatalogue.MatchInPage(multiple!, "lby", "Synthetic city", "Synthetic area", "branch-b").Branch?.Id == "branch-b",
            "Explicit provider branch identity disambiguates a page candidate");
        foreach (var invalid in new[] { "{}", "[]", "{\"results\":[null]}", json.Replace("branch-a", ""),
            json.Replace("\"lby\"", "\"LBY\""), json.Replace("\"deliveryRate\":0", "\"deliveryRate\":-1"),
            json.Replace("\"deliveryRate\":0", "\"deliveryRate\":\"0\""),
            "{\"results\":[" + branch + "," + branch + "]}", "{\"results\":[" + branch + "],\"totalCount\":0}" })
            check(!Branches(invalid), "Malformed branch catalogue fails without exposing a partial typed page");
        check(Branches(json.Replace("\"deliveryRate\":0", "\"deliveryRate\":null")),
            "Explicit null provider rate remains unavailable rather than being converted to free delivery");
        const string package = "{\"_id\":\"service-a\",\"title\":\"Synthetic service\",\"currency\":\"lyd\",\"amount\":2.5,\"isPrimary\":true}";
        check(SabilCatalogue.TryReadPackages(Parse("{\"results\":[" + package + "]}"), out var services) && services!.Packages[0].Amount == 2.5m,
            "Typed package retains its own service amount without equating it to a destination quote");
        foreach (var invalid in new[] { package.Replace("2.5", "-1"), package.Replace("true", "\"true\""), package.Replace("\"lyd\"", "\"\""),
            package.Replace("\"amount\":2.5,", ""), package + "," + package.Replace("service-a", "service-b") })
            check(!Packages("{\"results\":[" + invalid + "]}"), "Invalid package rate or ambiguous primary service is rejected");
        check(SabilCatalogue.TryReadPackages(Parse("{\"results\":[" + package.Replace(",\"isPrimary\":true", "") + "]}"), out var noPrimary) &&
            !noPrimary!.Packages[0].IsPrimary, "Missing primary flag never silently selects a default package");
    }
}
