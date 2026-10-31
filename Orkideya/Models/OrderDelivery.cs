using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models;

public sealed class OrderDelivery
{
    [Key] public int OrderId { get; set; }
    [MaxLength(16)] public required string Mode { get; set; }
    [MaxLength(120)] public string? CarrierName { get; set; }
    [MaxLength(100)] public string? ShipmentReference { get; set; }
    [MaxLength(16)] public required string State { get; set; }
    [ConcurrencyCheck] public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
