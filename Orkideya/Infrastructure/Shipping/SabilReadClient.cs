using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;

namespace Orkideya.Infrastructure.Shipping;

public sealed class SabilReadClient(HttpClient http, IOptions<SabilReadOptions> configuration) : ICarrierReadGateway
{
    public string Provider => "Sabil";



    // One explicit page, never a claim that the complete destination catalogue was fetched.
    public async Task<CarrierReadResult> ListBranchesAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (offset < 0 || limit is < 1 or > 2000) return new(CarrierReadState.InvalidRequest);
        var path = FormattableString.Invariant($"/api/local/branches/public/?offset={offset}&limit={limit}&includeTotalCount=true");
        var result = await ReadAsync(path, cancellationToken);
        if (result.State != CarrierReadState.Success) return result;
        var data = result.Data!.Value;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("results", out var rows) ||
            rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > limit ||
            rows.EnumerateArray().Any(row => row.ValueKind != JsonValueKind.Object))
            return new(CarrierReadState.InvalidResponse);
        if (data.TryGetProperty("totalCount", out var total) &&
            (total.ValueKind != JsonValueKind.Number || !total.TryGetInt32(out var count) || count < rows.GetArrayLength()))
            return new(CarrierReadState.InvalidResponse);
        return result;
    }
    public Task<CarrierReadResult> ListServicePackagesAsync(CancellationToken cancellationToken = default) =>
        ReadAsync("/api/local/service/rates/public/", cancellationToken);

    public Task<CarrierReadResult> GetShipmentAsync(string reference, CancellationToken cancellationToken = default) =>
        ValidReference(reference) ? ReadAsync("/api/local/shipments/?reference=" + Uri.EscapeDataString(reference), cancellationToken)
            : Task.FromResult(new CarrierReadResult(CarrierReadState.InvalidReference));

    public Task<CarrierReadResult> GetTimelineAsync(string reference, CancellationToken cancellationToken = default) =>
        ValidReference(reference) ? ReadAsync("/api/local/shipments/timeline/" + Uri.EscapeDataString(reference), cancellationToken)
            : Task.FromResult(new CarrierReadResult(CarrierReadState.InvalidReference));

    private static bool ValidReference(string? value) => value is { Length: > 0 and <= 100 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private Task<CarrierReadResult> ReadAsync(string path, CancellationToken cancellationToken) =>
        new SabilTransport(http, configuration).SendAsync(HttpMethod.Get, path, null, configuration.Value.ReadEnabled, cancellationToken);
}
