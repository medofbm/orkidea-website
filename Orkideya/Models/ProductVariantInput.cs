using System.ComponentModel.DataAnnotations;
namespace Orkideya.Models;

public sealed class ProductVariantInput : IValidatableObject
{
    public int ProductVariantId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "اختر منتجاً صحيحاً")]
    public int ProductId { get; set; }
    [Required(ErrorMessage = "الحجم مطلوب")]
    [StringLength(50, ErrorMessage = "وصف الحجم طويل جداً")]
    public string Size { get; set; } = "";
    public decimal Price { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price <= 0 || Price > 9999999999999999.99m || decimal.Round(Price, 2) != Price)
            yield return new ValidationResult("السعر يجب أن يكون موجباً وبحد أقصى رقمين بعد الفاصلة.", new[] { nameof(Price) });
    }
}
