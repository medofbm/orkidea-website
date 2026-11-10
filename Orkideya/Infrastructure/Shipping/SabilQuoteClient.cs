using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;

namespace Orkideya.Infrastructure.Shipping;

public sealed class SabilQuoteClient(HttpClient http, IOptions<SabilReadOptions> configuration) : ICarrierQuoteGateway
{
    public async Task<CarrierReadResult> CalculateAsync(CarrierQuoteRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = configuration.Value;
        if (!options.QuoteEnabled) return new(CarrierReadState.Disabled);
        var serviceId = request?.ServiceId ?? options.QuoteServiceId;
        if (serviceId is not { Length: 24 } || !serviceId.All(char.IsAsciiHexDigit))
            return new(CarrierReadState.Misconfigured);
        if (request is null || !Name(request.City) || !Name(request.Area) || request.Products is null ||
            request.Products.Count is < 1 or > 100 || request.Products.Any(p => p is null ||
                p.Quantity is < 1 or > 1000 || p.Amount is < 0 or > 999999999 || decimal.Round(p.Amount, 2) != p.Amount))
            return new(CarrierReadState.InvalidRequest);
        // Caller must resolve exact destination from the catalogue and supply server-trusted cart prices.
        // Account default origin and receiver payment match the bounded accepted preview cases.
        using var body = JsonContent.Create(new
        {
            service = serviceId, paymentBy = "receiver",
            to = new { countryCode = "lby", city = request.City, area = request.Area },
            isPickup = request.IsPickup,
            products = request.Products.Select(p => new { amount = p.Amount, quantity = p.Quantity, currency = "lyd", isChargeable = true }).ToArray()
        });
        var result = await new SabilTransport(http, configuration).SendAsync(HttpMethod.Post,
            "/api/local/shipments/calculate/shipping/", body, options.QuoteEnabled, cancellationToken);
        if (result.State != CarrierReadState.Success) return result;
        return SabilQuoteParser.TryRead(result.Data!.Value, out _) ? result : new(CarrierReadState.InvalidResponse);
    }
    private static bool Name(string? value) => value is { Length: > 0 and <= 200 } &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}
