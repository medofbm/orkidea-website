using System.ComponentModel.DataAnnotations;
namespace Orkideya.Models;

public sealed class CheckoutInput
{
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

    [Required(ErrorMessage = "الرجاء إدخال عنوان الشحن")]
    [StringLength(200, ErrorMessage = "العنوان طويل جداً")]
    public string ShippingAddress { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "الرجاء اختيار مدينة توصيل متاحة")]
    public int ShippingRateId { get; set; }
}
