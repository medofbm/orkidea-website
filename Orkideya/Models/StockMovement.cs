using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orkideya.Models;

public sealed class StockMovement
{
    public Guid StockMovementId { get; set; }
    public int StockAccountId { get; set; }
    public int? OrderId { get; set; }
    [MaxLength(16)] public required string Kind { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal OnHandDelta { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal ReservedDelta { get; set; }
    [MaxLength(500)] public required string Reason { get; set; }
    [MaxLength(450)] public required string ActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
