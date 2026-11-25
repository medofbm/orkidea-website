using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Infrastructure.Shipping;

/// <summary>Sabil rate import writes through the app's retrying SQL strategy and replaces every shipping rate.</summary>
internal static class ShippingRateImportScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        var first = new SabilRatePlan(new List<SabilRateRow>
        {
            new("طرابلس", "عين زارة", 15m), new("طرابلس", "الباعيش", 25m), new("بنغازي", "الكيش", 40m)
        }, 2, 3, 0, 0);
        await using (var db = open())
            check(await new SabilRateImport(null!, db).ApplyAsync(first), "Sabil rate import applies under the retrying execution strategy");
        await using (var db = open())
        {
            var rows = await db.ShippingRates.AsNoTracking().OrderBy(r => r.Price).ToListAsync();
            check(rows.Count == 3 && rows[0].CityName == "طرابلس — عين زارة" && rows[0].Region == "طرابلس" && rows[0].Price == 15m,
                "Sabil rate import stores city — area, region and price");
        }
        var second = new SabilRatePlan(new List<SabilRateRow> { new("مصراتة", "مصراتة", 30m) }, 1, 1, 0, 0);
        await using (var db = open())
        {
            check(await new SabilRateImport(null!, db).ApplyAsync(second), "Sabil rate re-import applies");
            check(!await new SabilRateImport(null!, db).ApplyAsync(new SabilRatePlan(new List<SabilRateRow>(), 0, 0, 0, 0)),
                "Sabil rate import refuses an empty plan");
        }
        await using (var db = open())
            check(await db.ShippingRates.CountAsync() == 1 && await db.ShippingRates.AnyAsync(r => r.CityName == "مصراتة — مصراتة"),
                "Sabil rate re-import replaces every previous rate and keeps the last good list");
    }
}
