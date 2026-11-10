using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orkideya.Models;

// Optional for historical/manual-rate orders; never synthesize carrier provenance for them.
public sealed class OrderShippingQuote
{
    [Key] public int OrderId { get; set; }
    [MaxLength(32)] public required string Provider { get; set; }
    [MaxLength(24)] public required string ServiceId { get; set; }
    [MaxLength(16)] public string? ServiceKind { get; set; }
    [MaxLength(128)] public required string BranchId { get; set; }
    [MaxLength(200)] public required string City { get; set; }
    [MaxLength(200)] public required string Area { get; set; }
    [MaxLength(128)] public required string OriginRevision { get; set; }
    [MaxLength(32)] public required string HandoffMode { get; set; }
    public bool IsPickup { get; set; }
    [MaxLength(3)] public required string Currency { get; set; }
    [Column(TypeName="decimal(18,2)")] public decimal ShippingFee { get; set; }
    public DateTimeOffset QuotedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
}
