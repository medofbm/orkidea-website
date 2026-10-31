using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models;

public sealed class OrderDeliveryEvent
{
    [Key] public Guid RequestId { get; set; }
    public int OrderId { get; set; }
    public int Revision { get; set; }
    [MaxLength(32)] public required string Action { get; set; }
    [MaxLength(16)] public string? PreviousState { get; set; }
    [MaxLength(16)] public required string NewState { get; set; }
    [MaxLength(16)] public required string Mode { get; set; }
    [MaxLength(120)] public string? CarrierName { get; set; }
    [MaxLength(100)] public string? ShipmentReference { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    [MaxLength(450)] public required string ActorUserId { get; set; }
    [MaxLength(64)] public required string RequestHash { get; set; }
    public bool LegacyPreparationConfirmed { get; set; }
    public bool ReturnReceiptConfirmed { get; set; }
    public bool SamePackageReuseConfirmed { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
