namespace Orkideya.Infrastructure.Shipping;

public sealed class SabilReadOptions
{
    public const string Section = "Carriers:Sabil";
    public bool ReadEnabled { get; set; } // Fail closed by default; this is not shipment creation permission.
    public string ApiKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 15;
}
