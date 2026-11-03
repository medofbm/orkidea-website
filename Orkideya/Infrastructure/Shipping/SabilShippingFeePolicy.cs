using System.Text.Json;
using Orkideya.Application.Shipping;

namespace Orkideya.Infrastructure.Shipping;

// Deliberately narrow policy for the accepted local receiver-paid quote shape.
// The order's product total comes from the server cart, never from summing provider invoices.
public static class SabilShippingFeePolicy
{
    public static bool TryGetShippingFee(JsonElement data, IReadOnlyList<CarrierQuoteProduct> expectedProducts, out decimal? fee)
    {
        fee = null;
        if (expectedProducts is null || expectedProducts.Count is < 1 or > 100 ||
            expectedProducts.Any(p => p is null || p.Quantity is < 1 or > 1000 ||
                !Money(p.Amount)) || !SabilQuoteParser.TryRead(data, out var quote) || quote!.Invoices.Count != 1)
            return false;
        var invoice = quote.Invoices[0];
        if (invoice.Currency != "lyd" || invoice.Items.Any(i => i.Type is not ("product" or "shipping"))) return false;
        var shipping = invoice.Items.Where(i => i.Type == "shipping").ToArray();
        if (shipping.Length != 1 || !Money(shipping[0].Amount) || shipping[0].Quantity is not (null or 1m)) return false;
        var products = invoice.Items.Where(i => i.Type == "product").ToArray();
        if (products.Length != expectedProducts.Count || products.Any(p => p.Quantity is null || !Money(p.Amount))) return false;
        // Compare a multiset, preserving repeated lines. Amount and quantity remain separate facts.
        var actual = products.Select(p => (p.Amount, Quantity: p.Quantity!.Value)).OrderBy(p => p.Amount).ThenBy(p => p.Quantity);
        var expected = expectedProducts.Select(p => (p.Amount, Quantity: (decimal)p.Quantity)).OrderBy(p => p.Amount).ThenBy(p => p.Quantity);
        if (!actual.SequenceEqual(expected)) return false;
        fee = shipping[0].Amount;
        return true;
    }
    private static bool Money(decimal value) => value is >= 0 and <= 999999999 && decimal.Round(value, 2) == value;
}
