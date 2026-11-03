using System.Text.Json;
using Orkideya.Application.Shipping;
using Orkideya.Infrastructure.Shipping;

internal static class SabilShippingFeeRegression
{
    public static void Run(Action<bool,string> check)
    {
        const string json="{\"invoices\":[{\"currency\":\"lyd\",\"items\":[{\"type\":\"product\",\"amount\":25,\"quantity\":2},{\"type\":\"shipping\",\"amount\":15}]}]}";
        CarrierQuoteProduct[] cart=[new(25,2)];
        JsonElement Parse(string s){using var d=JsonDocument.Parse(s);return d.RootElement.Clone();}
        check(SabilShippingFeePolicy.TryGetShippingFee(Parse(json),cart,out var fee)&&fee==15,
            "Accepted quote extracts shipping only, never the product amount or whole invoice total");
        check(SabilShippingFeePolicy.TryGetShippingFee(Parse(json.Replace("\"amount\":15","\"amount\":0")),cart,out fee)&&fee==0,
            "Explicit zero shipping is accepted only from a valid matching quote");
        check(SabilShippingFeePolicy.TryGetShippingFee(Parse(json.Replace("\"amount\":15","\"amount\":15,\"quantity\":1")),cart,out fee)&&fee==15,
            "Single shipping quantity does not multiply a fee");
        foreach(var bad in new[]{"{}",json.Replace("\"lyd\"","\"usd\""),json.Replace("\"shipping\"","\"tax\""),
            json.Replace("\"amount\":15","\"amount\":-1"),json.Replace("\"amount\":15","\"amount\":1.001"),
            json.Replace("\"amount\":15","\"amount\":15,\"quantity\":2"),
            json.Replace("\"amount\":25","\"amount\":50"),json.Replace("\"quantity\":2","\"quantity\":1"),
            json.Replace(",\"quantity\":2",""),json.Replace("\"amount\":15}","\"amount\":15},{\"type\":\"shipping\",\"amount\":5}"),
            json.Replace("\"amount\":15}","\"amount\":15},{\"type\":\"discount\",\"amount\":0}")})
            check(!SabilShippingFeePolicy.TryGetShippingFee(Parse(bad),cart,out fee)&&fee is null,
                "Unsupported or cart-mismatched quote yields no shipping price, including zero-value extra fees");
        check(!SabilShippingFeePolicy.TryGetShippingFee(Parse(json),[],out fee)&&fee is null,
            "Empty server cart cannot accept a quote fee");
        check(!SabilShippingFeePolicy.TryGetShippingFee(Parse(json),[new(25,2),new(25,2)],out fee)&&fee is null,
            "Missing repeated product row is not silently accepted");
        var two=json.Replace("{\"type\":\"product\",\"amount\":25,\"quantity\":2}",
            "{\"type\":\"product\",\"amount\":10,\"quantity\":1},{\"type\":\"product\",\"amount\":25,\"quantity\":2}");
        check(SabilShippingFeePolicy.TryGetShippingFee(Parse(two),[new(25,2),new(10,1)],out fee)&&fee==15,
            "Provider product order does not matter while all amount/quantity rows must match");
        var invoice=Parse(json).GetProperty("invoices")[0].GetRawText();
        check(!SabilShippingFeePolicy.TryGetShippingFee(Parse("{\"invoices\":["+invoice+","+invoice+"]}"),cart,out fee)&&fee is null,
            "Multiple invoices require explicit reconciliation rather than automatic fee summation");
    }
}
