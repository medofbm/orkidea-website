using System.Text.Json;
using Orkideya.Application.Shipping;
using Orkideya.Infrastructure.Shipping;

internal static class SabilRateImportRegression
{
    private sealed class FakeGateway(CarrierReadState state, params string[] pages) : ICarrierReadGateway
    {
        public string Provider => "fake";
        public List<(int Offset, int Limit)> Calls { get; } = [];
        public Task<CarrierReadResult> ListBranchesAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
        {
            Calls.Add((offset, limit));
            if (state != CarrierReadState.Success) return Task.FromResult(new CarrierReadResult(state));
            var index = Calls.Count - 1;
            using var doc = JsonDocument.Parse(index < pages.Length ? pages[index] : "{\"results\":[]}");
            return Task.FromResult(new CarrierReadResult(CarrierReadState.Success, doc.RootElement.Clone()));
        }
        public Task<CarrierReadResult> ListServicePackagesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CarrierReadResult> GetShipmentAsync(string reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CarrierReadResult> GetTimelineAsync(string reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static string Branch(string id, string city, string areas, string country = "lby", string currency = "lyd") =>
        $"{{\"_id\":\"{id}\",\"countryCode\":\"{country}\",\"city\":\"{city}\",\"currency\":\"{currency}\",\"areas\":[{areas}]}}";
    private static string Area(string name, string? rate) => $"{{\"area\":\"{name}\",\"deliveryRate\":{rate ?? "null"}}}";
    private static string Page(params string[] branches) => "{\"results\":[" + string.Join(",", branches) + "]}";

    public static async Task Run(Action<bool, string> check)
    {
        // Two pages: a full first page (100 branches) forces a second request at offset 100.
        var first = Page(Enumerable.Range(0, 100).Select(i => Branch("b" + i, "City" + (i % 3), Area("Area" + i, "10"))).ToArray());
        var second = Page(Branch("last", "City9", Area("Zed", "5.5") + "," + Area("NoRate", null)));
        var paged = new FakeGateway(CarrierReadState.Success, first, second);
        var result = await SabilRatePlanner.BuildAsync(paged);
        check(result.Succeeded && paged.Calls.SequenceEqual([(0, 100), (100, 100)]), "Rate import pages the catalogue until a short page");
        check(result.Plan!.Areas == 101 && result.Plan.Cities == 4 && result.Plan.SkippedNoRate == 1 && result.Plan.Conflicts == 0,
            "Rate import counts cities, areas and skipped null rates");
        check(result.Plan.Rows[0].City == "City0" && result.Plan.Rows.Last().City == "City9", "Rate plan is sorted by city");

        var dup = new FakeGateway(CarrierReadState.Success, Page(
            Branch("a", "Tripoli", Area("Center", "8") + "," + Area("Center", "12")),
            Branch("b", "Tripoli", Area("Center", "10") + "," + Area("Port", "7")),
            Branch("c", "Cairo", Area("Center", "3"), "egy", "egp"),
            Branch("d", "Tripoli", Area("Usd", "3"), "lby", "usd")));
        var plan = (await SabilRatePlanner.BuildAsync(dup)).Plan!;
        check(plan.Rows.Count == 2 && plan.Rows.Single(r => r.Area == "Center").Price == 12m && plan.Conflicts == 1,
            "Duplicate key with different rates keeps the highest rate and counts one conflict");
        check(plan.Rows.All(r => r.City == "Tripoli") && plan.Rows[0].Area == "Port", "Non-lyd or non-lby rows are skipped and rows sort by price");

        var same = (await SabilRatePlanner.BuildAsync(new FakeGateway(CarrierReadState.Success,
            Page(Branch("a", "T", Area("C", "8")), Branch("b", "T", Area("C", "8")))))).Plan!;
        check(same.Rows.Count == 1 && same.Conflicts == 0, "Identical duplicate rates are not a conflict");

        var off = await SabilRatePlanner.BuildAsync(new FakeGateway(CarrierReadState.Disabled));
        check(!off.Succeeded && off.State == CarrierReadState.Disabled && off.Plan is null, "Disabled gateway yields a failure state without a plan");
        var bad = await SabilRatePlanner.BuildAsync(new FakeGateway(CarrierReadState.Success, "{\"nope\":1}"));
        check(!bad.Succeeded && bad.State == CarrierReadState.InvalidResponse, "Unparseable catalogue yields InvalidResponse");

        // Delivery summary grouping and area label.
        check(DeliveryRateGroups.AreaLabel("Tripoli", "Tripoli — Center") == "Center", "Area label drops the leading region");
        check(DeliveryRateGroups.AreaLabel("Tripoli", "Misrata") == "Misrata" && DeliveryRateGroups.AreaLabel("Tripoli", "Tripoli — ") == "Tripoli — ",
            "Area label keeps names that do not carry the region prefix");
        var groups = DeliveryRateGroups.Build([("B", "B — z", 20m), ("B", "B — a", 20m), ("B", "B — m", 15m), ("A", "A — x", 30m), ("C", "plain", 10m)]);
        check(groups.Select(g => g.City).SequenceEqual(["C", "B", "A"]), "Groups are ordered by minimum price");
        check(groups[1].MinPrice == 15m && groups[1].MaxPrice == 20m && groups[1].Areas.Select(a => a.Area).SequenceEqual(["m", "a", "z"]),
            "Group areas are ordered by price then name with min and max");
    }
}
