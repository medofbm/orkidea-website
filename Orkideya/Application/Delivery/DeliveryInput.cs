using System.ComponentModel.DataAnnotations;

namespace Orkideya.Application.Delivery;

public sealed class DeliveryInput
{
    [Required, StringLength(32)] public string? Action { get; set; }
    [Required, Range(0, int.MaxValue)] public int? ExpectedRevision { get; set; }
    [Required] public Guid? RequestId { get; set; }
    [StringLength(16)] public string? Mode { get; set; }
    [StringLength(120)] public string? CarrierName { get; set; }
    [StringLength(100)] public string? ShipmentReference { get; set; }
    [StringLength(16)] public string? TargetState { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public bool ConfirmLegacyPrepared { get; set; }
    public bool ConfirmReturnReceived { get; set; }
    public bool ConfirmSamePackageReusable { get; set; }
}
