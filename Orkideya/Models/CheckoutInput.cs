using System.ComponentModel.DataAnnotations;
namespace Orkideya.Models;

public sealed class CheckoutInput : IValidatableObject
{
    [Required(ErrorMessage = "انتهت صلاحية تأكيد الطلب. افتح صفحة إتمام الطلب من جديد.")]
    public Guid? CheckoutToken { get; set; }
    [Required(ErrorMessage = "الرجاء إدخال رقم الواتساب")]
    [StringLength(20)]
    [RegularExpression(@"^\+?[0-9](?:[ ()\-]*[0-9]){6,14}[ ()\-]*$", ErrorMessage = "الرجاء إدخال رقم هاتف صحيح مع مفتاح الدولة عند الحاجة")]
    public string WhatsAppNumber { get; set; } = "";

    [Required(ErrorMessage = "الرجاء إدخال الاسم الأول")]
    [StringLength(50, ErrorMessage = "الاسم الأول طويل جداً")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "الرجاء إدخال اسم العائلة")]
    [StringLength(50, ErrorMessage = "اسم العائلة طويل جداً")]
    public string LastName { get; set; } = "";

    [StringLength(200, ErrorMessage = "العنوان طويل جداً")]
    public string ShippingAddress { get; set; } = "";

    [StringLength(128)] public string? CarrierDestination { get; set; }
    [StringLength(16384)] public string? QuoteToken { get; set; }

    public int ShippingRateId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ShippingRateId < 0 || (string.IsNullOrEmpty(CarrierDestination) && ShippingRateId == 0))
            yield return new ValidationResult("الرجاء اختيار مدينة توصيل متاحة", [nameof(ShippingRateId)]);
    }
}
