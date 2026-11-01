using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;

namespace Orkideya.Infrastructure.Shipping;

public sealed class SabilReadClient(HttpClient http, IOptions<SabilReadOptions> configuration) : ICarrierReadGateway
{
    public string Provider => "Sabil";
    private const int MaximumBodyBytes = 1024 * 1024;
    private static readonly Uri Origin = new("https://v2.sabil.ly");

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

    private static bool ValidHeader(string? value, int max) => value is { Length: > 0 } && value.Length <= max &&
        value.All(c => c is >= '!' and <= '~');

    private async Task<CarrierReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = configuration.Value;
        if (!options.ReadEnabled) return new(CarrierReadState.Disabled);
        if (!ValidHeader(options.ApiKey, 2048) || !ValidHeader(options.AccountId, 128) || options.TimeoutSeconds is < 1 or > 60)
            return new(CarrierReadState.Misconfigured);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Origin, path));
        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("apikey", options.ApiKey);
            request.Headers.Add("X-ACCOUNT-ID", options.AccountId);
            request.Headers.Add("X-API-VERSION", "1.0.0");
        }
        catch (FormatException) { return new(CarrierReadState.Misconfigured); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            // No retries and no POST/PUT/PATCH/DELETE capability. Never follow a redirect with credentials.
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return new(Map(response.StatusCode));
            if (response.Content.Headers.ContentLength > MaximumBodyBytes) return new(CarrierReadState.InvalidResponse);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(), timeout.Token);
                if (count == 0) break;
                if (body.Length + count > MaximumBodyBytes) return new(CarrierReadState.InvalidResponse);
                body.Write(buffer, 0, count);
            }
            using var json = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("status", out var status) ||
                status.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return new(CarrierReadState.InvalidResponse);
            if (!status.GetBoolean()) return new(CarrierReadState.Rejected);
            if (!root.TryGetProperty("data", out var data) || data.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
                return new(CarrierReadState.InvalidResponse);
            return new(CarrierReadState.Success, data.Clone());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(CarrierReadState.Unavailable); }
        catch (HttpRequestException) { return new(CarrierReadState.Unavailable); }
        catch (IOException) { return new(CarrierReadState.Unavailable); }
        catch (JsonException) { return new(CarrierReadState.InvalidResponse); }
        // Caller cancellation propagates. Provider messages, response bodies and exceptions are never logged or returned as errors.
    }

    private static CarrierReadState Map(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => CarrierReadState.Unauthorized,
        HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden => CarrierReadState.PermissionDenied,
        HttpStatusCode.NotFound => CarrierReadState.NotFound,
        HttpStatusCode.TooManyRequests => CarrierReadState.RateLimited,
        _ when (int)code is >= 400 and < 500 => CarrierReadState.Rejected,
        _ => CarrierReadState.Unavailable
    };
}
