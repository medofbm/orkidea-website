using System.Text.Json;

namespace Orkideya.Application.Shipping;

public enum CarrierReadState
{
    Success, Disabled, Misconfigured, InvalidReference, InvalidRequest, Unauthorized,
    PermissionDenied, NotFound, RateLimited, Rejected, Unavailable, InvalidResponse
}

// Provider data is deliberately opaque until the full status/response contract is established.
// Reading it never changes an order, inventory, delivery event or collection state.
public sealed class CarrierReadResult(CarrierReadState state, JsonElement? data = null)
{
    public CarrierReadState State { get; } = state;
    public JsonElement? Data { get; } = state == CarrierReadState.Success ? data : null;
    public override string ToString() => $"Carrier read: {State}";
}

public interface ICarrierReadGateway
{
    string Provider { get; }
    Task<CarrierReadResult> ListBranchesAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default);
    Task<CarrierReadResult> ListServicePackagesAsync(CancellationToken cancellationToken = default);
    Task<CarrierReadResult> GetShipmentAsync(string reference, CancellationToken cancellationToken = default);
    Task<CarrierReadResult> GetTimelineAsync(string reference, CancellationToken cancellationToken = default);
}
