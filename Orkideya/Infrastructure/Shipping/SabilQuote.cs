using System.Text.Json;

namespace Orkideya.Infrastructure.Shipping;

// Provider invoice facts only. These records deliberately expose no computed shipping/order total.
public sealed record SabilQuoteItem(string Type, decimal Amount, decimal? Quantity);
public sealed record SabilQuoteInvoice(string Currency, IReadOnlyList<SabilQuoteItem> Items);
public sealed record SabilQuote(IReadOnlyList<SabilQuoteInvoice> Invoices);

public static class SabilQuoteParser
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
        { "product", "charge", "refund", "package-charge", "shipping", "tax", "payment", "discount" };

    // Input is the successful provider envelope's data object, never the whole HTTP response.
    public static bool TryRead(JsonElement data, out SabilQuote? quote)
    {
        quote = null;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("invoices", out var invoices) ||
            invoices.ValueKind != JsonValueKind.Array || invoices.GetArrayLength() is < 1 or > 100) return false;
        var result = new List<SabilQuoteInvoice>();
        var count = 0;
        foreach (var invoice in invoices.EnumerateArray())
        {
            if (invoice.ValueKind != JsonValueKind.Object || !invoice.TryGetProperty("currency", out var currency) ||
                currency.ValueKind != JsonValueKind.String) return false;
            var code = currency.GetString()!;
            if (code.Length != 3 || !code.All(c => c is >= 'a' and <= 'z') ||
                !invoice.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() is < 1 or > 1000) return false;
            count += items.GetArrayLength();
            if (count > 2000) return false;
            var parsed = new List<SabilQuoteItem>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var type) ||
                    type.ValueKind != JsonValueKind.String || !Types.Contains(type.GetString()!) ||
                    !item.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number ||
                    !amount.TryGetDecimal(out var number)) return false;
                decimal? quantity = null;
                if (item.TryGetProperty("quantity", out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var q) || q < 0) return false;
                    quantity = q;
                }
                // Preserve sign and quantity separately; invoice fee allocation has a separate acceptance gate.
                parsed.Add(new(type.GetString()!, number, quantity));
            }
            result.Add(new(code, parsed.AsReadOnly()));
        }
        quote = new(result.AsReadOnly());
        return true;
    }
}
