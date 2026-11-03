using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Orkideya.Application.Shipping;

public sealed record QuoteCartLine(int VariantId, int Quantity, decimal UnitPrice);
public sealed record QuoteDestination(string BranchId, string City, string Area);
// OriginRevision is a server-owned configuration revision, never a customer-supplied address.
public sealed record QuoteBinding(Guid CheckoutToken, string ServiceId, string OriginRevision,
    string HandoffMode, QuoteDestination Destination, IReadOnlyList<QuoteCartLine> Lines);

public sealed class CheckoutQuoteContext(IDataProtectionProvider protection, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("Orkidea.CheckoutQuote.v1");
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private sealed record Ticket(string Fingerprint, decimal Fee, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

    public string Create(QuoteBinding binding, decimal acceptedFee)
    {
        if (!Valid(binding) || acceptedFee is < 0 or > 999999999 || decimal.Round(acceptedFee, 2) != acceptedFee)
            throw new ArgumentException("Invalid quote context.");
        var now = clock.GetUtcNow();
        return protector.Protect(JsonSerializer.Serialize(new Ticket(Fingerprint(binding), acceptedFee, now, now + Lifetime)));
    }
    public bool TryRead(string? token, QuoteBinding current, out decimal? fee)
    {
        fee = null;
        if (token is not { Length: > 0 and <= 16384 } || !Valid(current)) return false;
        try
        {
            var ticket = JsonSerializer.Deserialize<Ticket>(protector.Unprotect(token));
            var now = clock.GetUtcNow();
            if (ticket is null || ticket.IssuedAt > now || now >= ticket.ExpiresAt ||
                ticket.ExpiresAt - ticket.IssuedAt != Lifetime || ticket.Fingerprint != Fingerprint(current) ||
                ticket.Fee is < 0 or > 999999999 || decimal.Round(ticket.Fee, 2) != ticket.Fee) return false;
            fee = ticket.Fee;
            return true;
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException or ArgumentException)
        { return false; }
    }
    private static string Fingerprint(QuoteBinding value) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { value.CheckoutToken, value.ServiceId, value.OriginRevision,
            value.HandoffMode, value.Destination, Lines = value.Lines.OrderBy(l => l.VariantId).ToArray() })));
    private static bool Text(string? value, int max) => value is { Length: > 0 } && value.Length <= max &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    internal static bool Valid(QuoteBinding? value) => value is not null && value.CheckoutToken != Guid.Empty &&
        value.ServiceId is { Length: 24 } && value.ServiceId.All(char.IsAsciiHexDigit) && Text(value.OriginRevision,128) &&
        value.HandoffMode is "OfficeDropoff" or "CarrierCollection" && value.Destination is { } d &&
        Text(d.BranchId,128) && Text(d.City,200) && Text(d.Area,200) && value.Lines is { Count: > 0 and <= 100 } &&
        value.Lines.All(l => l is not null && l.VariantId > 0 && l.Quantity is > 0 and <= 1000 &&
            l.UnitPrice is >= 0 and <= 999999999 && decimal.Round(l.UnitPrice,2) == l.UnitPrice) &&
        value.Lines.Select(l => l.VariantId).Distinct().Count() == value.Lines.Count;
}
