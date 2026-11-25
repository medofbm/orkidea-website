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

        // Pickup rows, zero rates and the surcharge.
        var pick = (await SabilRatePlanner.BuildAsync(new FakeGateway(CarrierReadState.Success, Page(
            Branch("a", "Benghazi", Area("بنغازي - استلام من المكتب", "0") + "," + Area("حي الأندلس - إستلام مكتب", "5") + "," + Area("Free", "0")
                + "," + Area("أستلام", "4") + "," + Area("Ain Zara", "5") + "," + Area("NoRate", null)))))).Plan!;
        check(pick.Rows.Count == 1 && pick.Rows[0].Area == "Ain Zara" && pick.SkippedPickup == 4 && pick.SkippedNoRate == 1,
            "Pickup areas (both spellings) and zero rates are skipped and counted; null rate stays SkippedNoRate");

        var sur = (await SabilRatePlanner.BuildAsync(new FakeGateway(CarrierReadState.Success, Page(
            Branch("a", "T", Area("C", "8") + "," + Area("C", "12") + "," + Area("P", "5.5")),
            Branch("b", "T", Area("C", "10")))), 10m)).Plan!;
        check(sur.Surcharge == 10m && sur.Rows.Single(r => r.Area == "P").Price == 15.5m && sur.Rows.Single(r => r.Area == "C").Price == 22m && sur.Conflicts == 1,
            "Surcharge is added to every rate, including conflicts with highest-wins");
        var none = (await SabilRatePlanner.BuildAsync(new FakeGateway(CarrierReadState.Success, Page(Branch("a", "T", Area("P", "5")))))).Plan!;
        check(none.Surcharge == 0m && none.Rows[0].Price == 5m, "Default surcharge is zero");

        check(!SabilSurcharge.TryParse("-1", out _) && !SabilSurcharge.TryParse("101", out _) && !SabilSurcharge.TryParse("1.234", out _)
            && !SabilSurcharge.TryParse("abc", out _) && !SabilSurcharge.IsValid(-1m) && !SabilSurcharge.IsValid(101m) && !SabilSurcharge.IsValid(1.234m),
            "Surcharge validation rejects -1, 101, 1.234 and non-numbers");
        check(SabilSurcharge.TryParse("0", out var s0) && s0 == 0m && SabilSurcharge.TryParse("10.5", out var s1) && s1 == 10.5m
            && SabilSurcharge.TryParse("100", out var s2) && s2 == 100m && SabilSurcharge.TryParse(null, out var s3) && s3 == 0m,
            "Surcharge validation accepts 0, 10.5, 100 and empty");

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
