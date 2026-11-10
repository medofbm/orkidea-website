using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;
using Orkideya.Infrastructure.Shipping;

internal static class SabilCheckoutRegression
{
    public static async Task Run(Action<bool,string> check)
    {
        var fake=new Fake();var carrier=new SabilReadOptions{ReadEnabled=true,QuoteEnabled=true,QuoteServiceId=new string('a',24)};
        var settings=new SabilCheckoutOptions{OriginRevision="v1",HandoffMode="OfficeDropoff"};
        var protection=new CheckoutQuoteContext(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var service=new SabilCheckoutQuotes(fake,fake,protection,Options.Create(carrier),Options.Create(settings));
        var id=Guid.NewGuid();var dest=new QuoteDestination("branch-a","Synthetic city","Synthetic area");
        QuoteCartLine[] lines=[new(1,1,25)];
        check((await service.PrepareAsync(id,dest,lines)).State==CheckoutQuoteState.Disabled && fake.Reads==0 && fake.Quotes==0,
            "Checkout carrier orchestration defaults off without provider requests");settings.Enabled=true;
        var result=await service.PrepareAsync(id,dest,lines);
        check(result.State==CheckoutQuoteState.Success && result.ShippingFee==15 && result.Token is not null && fake.Reads==1 && fake.Quotes==1,
            "Orchestration validates destination, accepts fee and protects matching cart context");
        check(service.TryValidate(result.Token,id,dest,lines,out var fee)&&fee==15,"Prepared quote validates against current server context");
        var office = dest with { Service = "women", ServiceId = new string('b', 24), IsPickup = true };
        var quotesBeforeOffice = fake.Quotes;
        check((await service.PrepareAsync(id,office,lines)).State == CheckoutQuoteState.DestinationUnavailable &&
            fake.Quotes == quotesBeforeOffice, "A destination without an office pickup rate cannot be quoted for pickup");
        fake.Pickup = true;
        var officeQuote = await service.PrepareAsync(id,office,lines);
        check(officeQuote.State == CheckoutQuoteState.Success &&
            fake.LastQuote is { IsPickup: true } sent && sent.ServiceId == office.ServiceId &&
            service.ReadSnapshot(officeQuote.Token,id,office,lines) is { IsPickup: true, ServiceKind: "women" },
            "Office quote uses the approved service and preserves recipient pickup separately");
        check(!service.TryValidate(officeQuote.Token,id,office with { IsPickup = false },lines,out _) &&
            !service.TryValidate(officeQuote.Token,id,office with { Service = "men", ServiceId = new string('c', 24) },lines,out _),
            "A quote cannot be reused with a different pickup choice or service");
        check(!service.TryValidate(result.Token,id,dest,[new(1,2,25)],out fee)&&fee is null,"Changed server cart invalidates prepared quote without network");
        settings.OriginRevision="v2";
        check(!service.TryValidate(result.Token,id,dest,lines,out fee),"Changed administrative origin invalidates checkout quote");settings.OriginRevision="v1";
        var before=fake.Quotes;
        check((await service.PrepareAsync(id,dest with{Area="Missing"},lines)).State==CheckoutQuoteState.DestinationUnavailable && fake.Quotes==before,
            "Unknown destination cannot trigger a price preview");
        fake.Total=false;
        check((await service.PrepareAsync(id,dest,lines)).State==CheckoutQuoteState.DestinationUnavailable && fake.Quotes==before,
            "Unknown catalogue completeness blocks destination approval");fake.Total=true;
        fake.Duplicate=true;
        check((await service.PrepareAsync(id,dest,lines)).State==CheckoutQuoteState.DestinationUnavailable && fake.Quotes==before,
            "Same city/area across branches is rejected because quote API cannot carry branch identity");fake.Duplicate=false;
        fake.ExtraFee=true;
        check((await service.PrepareAsync(id,dest,lines)).State==CheckoutQuoteState.UnsupportedQuote,
            "Unsupported fee combination never creates a protected checkout quote");fake.ExtraFee=false;
        fake.Fail=true;
        check((await service.PrepareAsync(id,dest,lines)).State==CheckoutQuoteState.ProviderUnavailable,
            "Provider denial yields unavailable, never free checkout delivery");fake.Fail=false;
        fake.OnQuote=()=>settings.HandoffMode="CarrierCollection";
        check((await service.PrepareAsync(id,dest,lines)).State==CheckoutQuoteState.InvalidContext,
            "Configuration change during quote cannot sign an old-context fee");fake.OnQuote=null;
        settings.Enabled=false;
        check(!service.TryValidate(result.Token,id,dest,lines,out fee)&&fee is null,"Disabling carrier checkout invalidates token use");
    }
    private sealed class Fake:ICarrierReadGateway,ICarrierQuoteGateway
    {
        public int Reads,Quotes;public bool Total=true,Duplicate,ExtraFee,Fail;public Action? OnQuote;
        public bool Pickup;
        public CarrierQuoteRequest? LastQuote;
        public string Provider=>"Sabil";
        private static JsonElement Parse(string s){using var d=JsonDocument.Parse(s);return d.RootElement.Clone();}
        public Task<CarrierReadResult> ListBranchesAsync(int offset=0,int limit=100,CancellationToken cancellationToken=default)
        {
            Reads++;var branch="{\"_id\":\"branch-a\",\"countryCode\":\"lby\",\"currency\":\"lyd\",\"city\":\"Synthetic city\",\"areas\":[{\"area\":\"Synthetic area\",\"deliveryRate\":5"+(Pickup?",\"pickupRate\":0":"")+"}]}";
            return Task.FromResult(new CarrierReadResult(CarrierReadState.Success,Parse("{\"results\":["+branch+(Duplicate?","+branch.Replace("branch-a","branch-b"):"")+"]"+(Total?",\"totalCount\":"+(Duplicate?2:1):"")+"}")));
        }
        public Task<CarrierReadResult> CalculateAsync(CarrierQuoteRequest request,CancellationToken cancellationToken=default)
        {
            Quotes++;LastQuote=request;OnQuote?.Invoke();return Task.FromResult(Fail?new CarrierReadResult(CarrierReadState.PermissionDenied):new(CarrierReadState.Success,
                Parse("{\"invoices\":[{\"currency\":\"lyd\",\"items\":[{\"type\":\"product\",\"amount\":25,\"quantity\":1},{\"type\":\"shipping\",\"amount\":15}"+(ExtraFee?",{\"type\":\"tax\",\"amount\":1}":"")+"]}]}")));
        }
        public Task<CarrierReadResult> ListServicePackagesAsync(CancellationToken c=default)=>throw new NotSupportedException();
        public Task<CarrierReadResult> GetShipmentAsync(string r,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<CarrierReadResult> GetTimelineAsync(string r,CancellationToken c=default)=>throw new NotSupportedException();
    }
}
