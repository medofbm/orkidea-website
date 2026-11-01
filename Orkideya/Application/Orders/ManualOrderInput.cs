using System.ComponentModel.DataAnnotations;

namespace Orkideya.Application.Orders;

public sealed class ManualOrderInput
{
    [Required] public Guid? RequestId { get; set; }
    [Required(ErrorMessage = "أدخل اسم الزبون"), StringLength(100)] public string CustomerName { get; set; } = "";
    [Required(ErrorMessage = "أدخل رقم الهاتف"), StringLength(20)]
    [RegularExpression(@"^\+?[0-9](?:[ ()\-]*[0-9]){6,14}[ ()\-]*$", ErrorMessage = "أدخل رقم هاتف صحيح")]
    public string Phone { get; set; } = "";
    [Required(ErrorMessage = "أدخل العنوان"), StringLength(200)] public string Address { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "اختر مدينة توصيل متاحة")] public int ShippingRateId { get; set; }
    [MinLength(1), MaxLength(30)] public List<ManualOrderLineInput> Lines { get; set; } = [];
}

public sealed class ManualOrderLineInput
{
    [Range(1, int.MaxValue, ErrorMessage = "اختر منتجاً وحجماً متاحاً")] public int VariantId { get; set; }
    [Range(1, 99, ErrorMessage = "الكمية من 1 إلى 99")] public int Quantity { get; set; } = 1;
}

public sealed record ManualDraftLine(int VariantId, int ProductId, string ProductName, string Size, int Quantity, decimal UnitPrice,
    int? StockAccountId, decimal? StockUnitsPerItem, string? StockUnit);

public sealed record ManualOrderDraft(Guid RequestId, string ActorId, ManualOrderInput Input, string City, decimal ShippingCost, List<ManualDraftLine> Lines)
{
    public string Fingerprint { get; init; } = "";
    public decimal ProductsTotal => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public decimal Total => ProductsTotal + ShippingCost;
}

public sealed record ManualPreview(ManualOrderDraft? Draft, string? Error);
public enum ManualSaveState { Created, Existing, ReviewChanged, Invalid }
public sealed record ManualSaveResult(ManualSaveState State, int? OrderId = null, ManualOrderDraft? Draft = null, string? Error = null);
