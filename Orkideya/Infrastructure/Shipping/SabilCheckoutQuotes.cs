using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;

namespace Orkideya.Infrastructure.Shipping;

public sealed class SabilCheckoutOptions
{
    public const string Section = "Carriers:Sabil:Checkout";
    public bool Enabled { get; set; }
    public Dictionary<string, QuoteDestination> HomeDestinations { get; set; } = new(StringComparer.Ordinal);
    public string OriginRevision { get; set; } = "";
    public string HandoffMode { get; set; } = "";
}
public enum CheckoutQuoteState { Success, Disabled, InvalidContext, DestinationUnavailable, ProviderUnavailable, UnsupportedQuote }
public sealed record PreparedCheckoutQuote(CheckoutQuoteState State, string? Token = null, decimal? ShippingFee = null);

// No public endpoint: caller must load cart prices from the database and authorize the checkout session.
public sealed class SabilCheckoutQuotes(ICarrierReadGateway catalogue, ICarrierQuoteGateway quotes,
    CheckoutQuoteContext protection, IOptions<SabilReadOptions> carrier, IOptions<SabilCheckoutOptions> checkout)
{
    public bool CheckoutEnabled => checkout.Value.Enabled;
    public IReadOnlyDictionary<string, QuoteDestination> HomeDestinations => checkout.Value.HomeDestinations;
    private bool Enabled => checkout.Value.Enabled && carrier.Value.ReadEnabled && carrier.Value.QuoteEnabled;
    private QuoteBinding Binding(Guid token, QuoteDestination destination, IReadOnlyList<QuoteCartLine> lines) =>
        new(token, carrier.Value.QuoteServiceId, checkout.Value.OriginRevision, checkout.Value.HandoffMode, destination, lines);

    public async Task<PreparedCheckoutQuote> PrepareAsync(Guid checkoutToken, QuoteDestination destination,
        IReadOnlyList<QuoteCartLine> serverLines, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enabled) return new(CheckoutQuoteState.Disabled);
        var binding = Binding(checkoutToken, destination, serverLines);
        if (!CheckoutQuoteContext.Valid(binding)) return new(CheckoutQuoteState.InvalidContext);
        // Freeze caller-owned collections before awaiting provider traffic.
        binding = binding with { Lines = binding.Lines.ToArray() };
        var response = await catalogue.ListBranchesAsync(0,2000,cancellationToken);
        if (response.State != CarrierReadState.Success) return new(CheckoutQuoteState.ProviderUnavailable);
        if (!SabilCatalogue.TryReadBranches(response.Data!.Value,out var page) || page!.TotalCount != page.Branches.Count)
            return new(CheckoutQuoteState.DestinationUnavailable);
        // Quote API accepts city/area, not branch ID: even an explicit branch cannot resolve duplicate names there.
        var match = SabilCatalogue.MatchInPage(page,"lby",binding.Destination.City,binding.Destination.Area);
        if (match.State != SabilDestinationState.SingleCandidate || match.Branch!.Id != binding.Destination.BranchId || match.Branch.Currency != "lyd")
            return new(CheckoutQuoteState.DestinationUnavailable);
        var products = binding.Lines.Select(l => new CarrierQuoteProduct(l.UnitPrice,l.Quantity)).ToArray();
        response = await quotes.CalculateAsync(new(binding.Destination.City,binding.Destination.Area,products),cancellationToken);
        if (response.State != CarrierReadState.Success) return new(CheckoutQuoteState.ProviderUnavailable);
        if (!SabilShippingFeePolicy.TryGetShippingFee(response.Data!.Value,products,out var fee)) return new(CheckoutQuoteState.UnsupportedQuote);
        // Changed administrative configuration during a provider call cannot sign a mismatched context.
        var current = Binding(checkoutToken,destination,binding.Lines);
        if (!Enabled || current != binding) return new(CheckoutQuoteState.InvalidContext);
        return new(CheckoutQuoteState.Success,protection.Create(binding,fee!.Value),fee);
    }
    public Orkideya.Models.OrderShippingQuote? ReadSnapshot(string? token, Guid checkoutToken, QuoteDestination destination, IReadOnlyList<QuoteCartLine> serverLines)
    {
        var binding = Binding(checkoutToken, destination, serverLines);
        if (!Enabled || !protection.TryReadDetails(token, binding, out var quote) || quote is null) return null;
        return new()
        {
            Provider = "Sabil", ServiceId = binding.ServiceId, BranchId = destination.BranchId,
            City = destination.City, Area = destination.Area, OriginRevision = binding.OriginRevision,
            HandoffMode = binding.HandoffMode, Currency = "lyd", ShippingFee = quote.Fee,
            QuotedAtUtc = quote.IssuedAt, ExpiresAtUtc = quote.ExpiresAt
        };
    }

    public bool TryValidate(string? token, Guid checkoutToken, QuoteDestination destination,
        IReadOnlyList<QuoteCartLine> serverLines,out decimal? fee)
    {
        fee=null;
        return Enabled && protection.TryRead(token,Binding(checkoutToken,destination,serverLines),out fee);
    }
}
