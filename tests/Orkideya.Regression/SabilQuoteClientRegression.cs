using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Orkideya.Application.Shipping;
using Orkideya.Infrastructure.Shipping;

internal static class SabilQuoteClientRegression
{
    public static async Task Run(Action<bool, string> check)
    {
        var options = new SabilReadOptions { ApiKey = "synthetic", AccountId = "synthetic-account", QuoteServiceId = new string('a',24) };
        using var handler = new Handler(); using var http = new HttpClient(handler);
        var gateway = new SabilQuoteClient(http, Options.Create(options));
        var request = new CarrierQuoteRequest("Synthetic city", "Synthetic area", [new(25m,2)]);
        check((await gateway.CalculateAsync(request)).State == CarrierReadState.Disabled && handler.Calls == 0,
            "Quote feature is independently disabled by default without network traffic");
        options.QuoteEnabled = true;
        var result = await gateway.CalculateAsync(request);
        check(result.State == CarrierReadState.Success && handler.Calls == 1 && handler.Uri == "https://v2.sabil.ly/api/local/shipments/calculate/shipping/" && handler.Method == HttpMethod.Post,
            "Quote gateway sends exactly one preview POST to the fixed provider path");
        using var body = JsonDocument.Parse(handler.Body!); var root=body.RootElement;
        check(root.GetProperty("service").GetString() == options.QuoteServiceId && root.GetProperty("paymentBy").GetString() == "receiver" &&
            root.GetProperty("products")[0].GetProperty("amount").GetDecimal() == 25 && root.GetProperty("products")[0].GetProperty("quantity").GetInt32() == 2,
            "Administrative service and unchanged monetary/quantity fields are serialized without arithmetic guesses");
        check(!root.TryGetProperty("contacts",out _) && !root.TryGetProperty("from",out _) && !root.GetProperty("to").TryGetProperty("address",out _),
            "Quote payload excludes contacts, personal address and shipment creation details");
        check(handler.Auth == "apikey synthetic" && handler.Version == "1.0.0", "Quote preview uses documented authentication/version headers");
        var officeService = new string('b', 24);
        check((await gateway.CalculateAsync(request with { ServiceId = officeService, IsPickup = true })).State == CarrierReadState.Success,
            "The provider transport accepts a server-selected service and office pickup");
        using var officeBody = JsonDocument.Parse(handler.Body!);
        check(officeBody.RootElement.GetProperty("service").GetString() == officeService &&
            officeBody.RootElement.GetProperty("isPickup").GetBoolean() &&
            !officeBody.RootElement.GetProperty("to").TryGetProperty("isPickup", out _),
            "Office pickup is sent as a top-level flag, separate from destination");
        foreach (var invalid in new[] { request with { City = "" }, request with { Area = "bad\narea" }, request with { Products = [] },
            request with { Products = [new(-1,1)] }, request with { Products = [new(1,0)] }, request with { Products = [new(1,1001)] }, request with { Products = [new(1.001m,1)] } })
        {
            var before=handler.Calls;
            check((await gateway.CalculateAsync(invalid)).State == CarrierReadState.InvalidRequest && handler.Calls == before,
                "Invalid quote inputs stop before contacting carrier");
        }
        options.QuoteServiceId = "invalid";var calls=handler.Calls;
        check((await gateway.CalculateAsync(request)).State == CarrierReadState.Misconfigured && handler.Calls == calls,
            "Missing approved service configuration blocks quote requests"); options.QuoteServiceId = new string('a',24);
        handler.Status=HttpStatusCode.PaymentRequired;
        check((await gateway.CalculateAsync(request)).State == CarrierReadState.PermissionDenied && handler.Calls == calls+1,
            "Quote permission denial is sanitized and never retried");handler.Status=HttpStatusCode.OK;
        handler.Response="{\"status\":true,\"data\":{\"invoices\":[]}}";
        check((await gateway.CalculateAsync(request)).State == CarrierReadState.InvalidResponse,
            "Successful HTTP quote with incomplete invoice facts is rejected");
        handler.Failure=new TaskCanceledException("private timeout");
        check((await gateway.CalculateAsync(request)).State == CarrierReadState.Unavailable,
            "Quote timeout returns unavailable rather than a zero price");
        using var canceled=new CancellationTokenSource();canceled.Cancel();calls=handler.Calls;var propagated=false;
        try { await gateway.CalculateAsync(request,canceled.Token); } catch(OperationCanceledException) { propagated=true; }
        check(propagated && calls==handler.Calls,"Canceled quote sends no provider request");
        using var provider=new ServiceCollection().AddLogging().AddCarrierReadGateway(new ConfigurationBuilder().Build()).BuildServiceProvider();
        check((await provider.GetRequiredService<ICarrierQuoteGateway>().CalculateAsync(request)).State==CarrierReadState.Disabled,
            "Application quote DI remains disabled without explicit configuration");
        var transport=provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(ICarrierQuoteGateway));
        while(transport is DelegatingHandler d) transport=d.InnerHandler!;
        check(transport is HttpClientHandler {AllowAutoRedirect:false,UseCookies:false},"Registered quote transport disables redirects and cookie sharing");
    }
    private sealed class Handler:HttpMessageHandler
    {
        public int Calls; public string? Uri,Body,Auth,Version; public HttpMethod? Method;
        public HttpStatusCode Status=HttpStatusCode.OK;public Exception? Failure;
        public string Response="{\"status\":true,\"data\":{\"invoices\":[{\"currency\":\"lyd\",\"items\":[{\"type\":\"shipping\",\"amount\":15}]}]}}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;Uri=request.RequestUri!.ToString();Method=request.Method;Body=await request.Content!.ReadAsStringAsync(token);
            Auth=request.Headers.Authorization?.ToString();Version=request.Headers.GetValues("X-API-VERSION").Single();
            if(Failure is not null) throw Failure;
            return new(Status){Content=new StringContent(Response)};
        }
    }
}
