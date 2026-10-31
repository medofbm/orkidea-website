using System.ComponentModel.DataAnnotations;
using Orkideya.Models;

namespace Orkideya.Areas.Admin.Models;

public sealed class StockIndexViewModel
{
    public List<Product> Products { get; set; } = [];
    public Dictionary<int, StockAccount> Accounts { get; set; } = [];
}
public sealed class StockDetailsViewModel
{
    public required Product Product { get; set; }
    public required StockAccount Account { get; set; }
    public List<StockMovement> Movements { get; set; } = [];
    public Dictionary<string, string> Actors { get; set; } = [];
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public int Page { get; set; } = 1;
    public int TotalMovements { get; set; }
}
public sealed class StockAdjustmentInput
{
    [Required] public decimal? Delta { get; set; }
    [Required, Range(0, int.MaxValue)] public int? ExpectedRevision { get; set; }
    [Required] public Guid? RequestId { get; set; }
    [Required, MaxLength(500)] public string Reason { get; set; } = "";
}
public sealed class StockVariantInput
{
    [Range(1, int.MaxValue)] public int VariantId { get; set; }
    [Required] public decimal? Units { get; set; }
    [Required, Range(0, int.MaxValue)] public int? ExpectedRevision { get; set; }
}
