using System.Text.Json;
using Orkideya.Infrastructure.Shipping;

internal static class SabilQuoteRegression
{
    public static void Run(Action<bool, string> check)
    {
        const string valid = "{\"invoices\":[{\"currency\":\"lyd\",\"items\":[{\"type\":\"product\",\"amount\":25,\"quantity\":2},{\"type\":\"shipping\",\"amount\":15}]}]}";
        JsonElement Parse(string json) { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
        check(SabilQuoteParser.TryRead(Parse(valid), out var quote) && quote!.Invoices[0].Items[0] is { Amount: 25, Quantity: 2 },
            "Live-shaped quote preserves product amount and quantity without assuming a line total");
        check(quote!.Invoices[0].Items[1] is { Type: "shipping", Amount: 15, Quantity: null },
            "Shipping amount is distinct from product amount and missing quantity stays unknown");
        check(SabilQuoteParser.TryRead(Parse(valid.Replace("\"amount\":15", "\"amount\":0")), out var free) && free!.Invoices[0].Items[1].Amount == 0,
            "Explicit zero quote amount remains different from a missing amount");
        foreach (var bad in new[] { "{}", "[]", "{\"invoices\":[]}", "{\"invoices\":[null]}",
            valid.Replace("\"lyd\"", "\"LYD\""), valid.Replace("\"items\":[", "\"items\":null,\"ignored\":["),
            valid.Replace("\"amount\":15", "\"amount\":\"15\""), valid.Replace("\"amount\":15", "\"amount\":null"),
            valid.Replace("\"amount\":15", "\"other\":15"), valid.Replace("\"shipping\"", "\"unknown-fee\""),
            valid.Replace("\"quantity\":2", "\"quantity\":-1"), valid.Replace("\"quantity\":2", "\"quantity\":\"2\""),
            valid.Replace("\"amount\":15", "\"amount\":1e100") })
            check(!SabilQuoteParser.TryRead(Parse(bad), out var rejected) && rejected is null,
                "Invalid invoice facts fail closed without returning a partial quote");
        check(SabilQuoteParser.TryRead(Parse(valid.Replace("\"shipping\",\"amount\":15", "\"discount\",\"amount\":-5")), out var discount) &&
            discount!.Invoices[0].Items[1].Amount == -5, "Discount sign is preserved without guessing invoice fee arithmetic");
        var rows = string.Join(",", Enumerable.Repeat("{\"type\":\"shipping\",\"amount\":1}", 1001));
        check(!SabilQuoteParser.TryRead(Parse("{\"invoices\":[{\"currency\":\"lyd\",\"items\":[" + rows + "]}]}"), out _),
            "Quote invoice item bound rejects excessive provider data");
    }
}
