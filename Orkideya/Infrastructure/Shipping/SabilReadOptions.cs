namespace Orkideya.Infrastructure.Shipping;

public sealed class SabilReadOptions
{
    public const string Section = "Carriers:Sabil";
    public bool ReadEnabled { get; set; } // Fail closed by default; this is not shipment creation permission.
    public bool QuoteEnabled { get; set; } // Separate opt-in; never enables shipment creation.
    public string QuoteServiceId { get; set; } = ""; // Administrative configuration only.
    public string ApiKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 15;
}
