namespace Orkideya.Application.Shipping;

// Amount is transmitted unchanged; invoice unit/line semantics are not inferred here.
public sealed record CarrierQuoteProduct(decimal Amount, int Quantity);
public sealed record CarrierQuoteRequest(string City, string Area, IReadOnlyList<CarrierQuoteProduct> Products);
public interface ICarrierQuoteGateway
{
    Task<CarrierReadResult> CalculateAsync(CarrierQuoteRequest request, CancellationToken cancellationToken = default);
}
