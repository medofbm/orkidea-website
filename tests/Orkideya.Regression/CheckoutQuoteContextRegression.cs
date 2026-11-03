using Microsoft.AspNetCore.DataProtection;
using Orkideya.Application.Shipping;

internal static class CheckoutQuoteContextRegression
{
    public static void Run(Action<bool,string> check)
    {
        var clock=new Clock();var protection=new EphemeralDataProtectionProvider();
        var context=new CheckoutQuoteContext(protection,clock);
        var binding=new QuoteBinding(Guid.NewGuid(),new string('a',24),"origin-v1","OfficeDropoff",new("branch-a","Synthetic city","Synthetic area"),[new(1,2,25),new(2,1,10)]);
        var token=context.Create(binding,15);
        check(context.TryRead(token,binding,out var fee)&&fee==15,"Protected quote restores only the accepted shipping fee");
        check(context.TryRead(token,binding with{Lines=binding.Lines.Reverse().ToArray()},out fee)&&fee==15,
            "Reordering identical server cart lines does not invalidate a quote");
        check(context.TryRead(token, binding with { Lines = [new(1, 2, 25.00m), new(2, 1, 10.00m)] }, out fee) && fee == 15,
            "SQL decimal scale does not invalidate a numerically identical cart price");
        foreach(var changed in new[]{binding with{CheckoutToken=Guid.NewGuid()},binding with{ServiceId=new string('b',24)},
            binding with{OriginRevision="origin-v2"},binding with{HandoffMode="CarrierCollection"},
            binding with{Destination=binding.Destination with{BranchId="branch-b"}},
            binding with{Destination=binding.Destination with{City="Other city"}},binding with{Destination=binding.Destination with{Area="Other area"}},
            binding with{Lines=[new(1,3,25),new(2,1,10)]},binding with{Lines=[new(1,2,26),new(2,1,10)]},binding with{Lines=[new(1,2,25)]}})
            check(!context.TryRead(token,changed,out fee)&&fee is null,"Changed cart, checkout or delivery context requires a fresh quote");
        check(!context.TryRead("tampered"+token,binding,out fee)&&fee is null,"Tampered quote token never becomes a zero shipping fee");
        check(!new CheckoutQuoteContext(new EphemeralDataProtectionProvider(),clock).TryRead(token,binding,out fee),
            "Different data-protection keys cannot validate a quote token");
        clock.Now=clock.Now.AddMinutes(5);
        check(!context.TryRead(token,binding,out fee)&&fee is null,"Quote expires at the five-minute boundary");
        clock.Now=clock.Now.AddMinutes(-6);
        check(!context.TryRead(token,binding,out fee)&&fee is null,"Future-issued quote is rejected after clock rollback");
        var invalid=false;try{context.Create(binding with{Lines=[new(1,1,25),new(1,2,25)]},15);}catch(ArgumentException){invalid=true;}
        check(invalid,"Duplicate server variant lines must be normalized before creating a quote context");
    }
    private sealed class Clock:TimeProvider
    {
        public DateTimeOffset Now=new(2026,11,3,12,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>Now;
    }
}
