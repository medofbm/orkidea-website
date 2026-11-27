using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orkideya.Models;

/// <summary>
/// Owner-controlled free-delivery promotion (for example «عرض الجمعة البيضاء»).
/// One row is enough: the admin page edits the single configuration. Nothing is seeded.
/// The server alone decides whether a website order gets free delivery (see DeliveryPromotionRule).
/// </summary>
public sealed class DeliveryPromotion
{
    public const string ScopeTripoli = "Tripoli";
    public const string ScopeAllLibya = "AllLibya";

    [Key] public int Id { get; set; }
    [MaxLength(80)] public required string Title { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    [MaxLength(16)] public string Scope { get; set; } = ScopeTripoli;
    [Column(TypeName = "decimal(18,2)")] public decimal? MinSubtotal { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [MaxLength(450)] public string? UpdatedBy { get; set; }
}
