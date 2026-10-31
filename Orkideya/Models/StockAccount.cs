using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orkideya.Models;

public sealed class StockAccount
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int ProductId { get; set; }
    [MaxLength(8)] public required string Unit { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal OnHand { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal Reserved { get; set; }
    [ConcurrencyCheck] public int Revision { get; set; }
}
