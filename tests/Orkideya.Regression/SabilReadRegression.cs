using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;
using Orkideya.Infrastructure.Shipping;

internal static class SabilReadRegression
{
    public static async Task Run(Action<bool, string> check)
    {
        var handler = new FakeHandler(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://not-the-carrier.invalid") };
        var options = new SabilReadOptions(); var gateway = new SabilReadClient(http, Options.Create(options));
        var disabled = await gateway.ListServicePackagesAsync();
        check(disabled.State == CarrierReadState.Disabled && handler.Calls == 0, "Carrier read gateway defaults disabled with zero outbound requests");
        options.ReadEnabled = true;
        check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.Misconfigured && handler.Calls == 0, "Enabled carrier without secrets fails closed before transport");
        options.ApiKey = "synthetic-only-key"; options.AccountId = "synthetic-test-account";
        options.ApiKey = "synthetic\r\nX-Injected: bad";
        check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.Misconfigured && handler.Calls == 0, "Carrier rejects credential header injection without sending");
        options.ApiKey = "synthetic-only-key"; options.TimeoutSeconds = 0;
        check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.Misconfigured && handler.Calls == 0, "Carrier timeout must be bounded"); options.TimeoutSeconds = 15;
        foreach (var reference in new[] { "", "../orders", "A?x=1", "https://other.invalid", "A B", new string('a', 101) })
            check((await gateway.GetShipmentAsync(reference)).State == CarrierReadState.InvalidReference && handler.Calls == 0, "Carrier rejects unsafe or invalid tracking reference");
        var result = await gateway.ListServicePackagesAsync();
        check(new CarrierReadResult(CarrierReadState.Unavailable, result.Data).Data == null, "Carrier error result cannot carry a provider payload");
        check(result.State == CarrierReadState.Success && result.Data!.Value.GetProperty("reference").GetString() == "QA-123", "Carrier successful envelope data survives response/document disposal");
        check(handler.LastUri == "https://v2.sabil.ly/api/local/service/rates/public/" && handler.LastMethod == HttpMethod.Get, "Carrier pins HTTPS provider origin and uses documented read-only package route");
        check(handler.LastAuthorization == "apikey synthetic-only-key" && handler.LastVersion == "1.0.0" && handler.LastAccount == "synthetic-test-account", "Carrier authenticates each request using documented headers");
        check(http.DefaultRequestHeaders.Authorization == null && !http.DefaultRequestHeaders.Contains("X-ACCOUNT-ID"), "Carrier secrets do not contaminate shared default headers");
        await gateway.GetShipmentAsync("QA-123"); check(handler.LastUri == "https://v2.sabil.ly/api/local/shipments/?reference=QA-123", "Shipment lookup uses provider reference without caller-controlled URL");
        await gateway.GetTimelineAsync("QA-123"); check(handler.LastUri == "https://v2.sabil.ly/api/local/shipments/timeline/QA-123", "Timeline lookup uses documented read route");
        foreach (var pair in new[]{(401,CarrierReadState.Unauthorized),(402,CarrierReadState.PermissionDenied),(403,CarrierReadState.PermissionDenied),(404,CarrierReadState.NotFound),
            (429,CarrierReadState.RateLimited),(400,CarrierReadState.Rejected),(503,CarrierReadState.Unavailable),(302,CarrierReadState.Unavailable)})
        {
            handler.Reply = () => new HttpResponseMessage((HttpStatusCode)pair.Item1) { Content = new StringContent("SYNTHETIC_PRIVATE_PROVIDER_PAYLOAD") };
            var count = handler.Calls; var failed = await gateway.GetShipmentAsync("QA-123");
            check(failed.State == pair.Item2 && failed.Data == null && handler.Calls == count + 1 && !failed.ToString().Contains("PRIVATE"), "Carrier HTTP " + pair.Item1 + " maps safely without payload leaks or retries");
        }
        foreach (var body in new[] { "not-json", "[]", "{\"status\":\"true\",\"data\":{}}", "{\"status\":true}", "{\"status\":true,\"data\":null}" })
        {
            handler.Reply = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.InvalidResponse, "Carrier rejects malformed or incomplete successful envelope");
        }
        handler.Reply = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":false,\"messages\":[{\"message\":\"SYNTHETIC_PRIVATE_PROVIDER_PAYLOAD\"}]}") };
        result = await gateway.ListServicePackagesAsync(); check(result.State == CarrierReadState.Rejected && result.Data == null && !result.ToString().Contains("PRIVATE"), "Carrier business rejection does not expose provider message contents");
        handler.Reply = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 1024 * 1024 + 1)) };
        check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.InvalidResponse, "Carrier rejects oversized declared response");
        handler.Reply = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(new byte[1024 * 1024 + 1]) };
        check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.InvalidResponse, "Carrier bounds streamed response without a content length");
        handler.Failure = new HttpRequestException("SYNTHETIC_PRIVATE_TRANSPORT_DETAIL"); var before = handler.Calls;
        result = await gateway.ListServicePackagesAsync(); check(result.State == CarrierReadState.Unavailable && result.Data == null && handler.Calls == before + 1, "Carrier network failure is sanitized and not retried");
        handler.Failure = new TaskCanceledException("Synthetic timeout");
        check((await gateway.ListServicePackagesAsync()).State == CarrierReadState.Unavailable, "Carrier transport timeout returns bounded unavailable state"); handler.Failure = null;
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); before = handler.Calls; var propagated = false;
        try { await gateway.ListServicePackagesAsync(canceled.Token); } catch (OperationCanceledException) { propagated = true; }
        check(propagated && handler.Calls == before, "Caller cancellation propagates without sending a request");
        using var provider = new ServiceCollection().AddLogging().AddCarrierReadGateway(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var registered = provider.GetRequiredService<ICarrierReadGateway>();
        check((await registered.ListServicePackagesAsync()).State == CarrierReadState.Disabled, "Application DI registration is fail-closed without configuration");
        var factory = provider.GetRequiredService<IHttpMessageHandlerFactory>(); var registeredHandler = factory.CreateHandler(nameof(ICarrierReadGateway));
        while (registeredHandler is DelegatingHandler delegating) registeredHandler = delegating.InnerHandler!;
        check(registeredHandler is HttpClientHandler { AllowAutoRedirect: false, UseCookies: false }, "Registered carrier transport disables redirects and cookie sharing");
        check(typeof(ICarrierReadGateway).GetMethods().All(m => m.Name is "get_Provider" or "ListServicePackagesAsync" or "GetShipmentAsync" or "GetTimelineAsync"),
            "First carrier boundary exposes no shipment creation, wallet or mutation operations");
    }
    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls; public string? LastUri, LastAuthorization, LastVersion, LastAccount; public HttpMethod? LastMethod; public Exception? Failure;
        public Func<HttpResponseMessage> Reply = () => new(HttpStatusCode.OK) { Content = new StringContent("{\"status\":true,\"data\":{\"reference\":\"QA-123\"}}") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; LastUri = request.RequestUri!.ToString(); LastMethod = request.Method; LastAuthorization = request.Headers.Authorization?.ToString();
            LastVersion = request.Headers.GetValues("X-API-VERSION").Single(); LastAccount = request.Headers.GetValues("X-ACCOUNT-ID").Single();
            return Failure is { } exception ? Task.FromException<HttpResponseMessage>(exception) : Task.FromResult(Reply());
        }
    }
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
