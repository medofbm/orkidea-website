namespace Orkideya.Application.Delivery;

public static class DeliveryLabels
{
    public static string State(string? state) => state switch
    {
        "HandedOver" => "مع جهة التوصيل", "Delivered" => "استلمه العميل",
        "Returning" => "لم يستلم — بانتظار الراجع", "Returned" => "استلمنا الراجع",
        null => "لم تسجل شحنة", _ => "حالة غير معروفة"
    };
    public static string Action(string action) => action switch
    {
        "HandOver" => "تسليم الطلب لجهة التوصيل", "Deliver" => "تأكيد استلام العميل",
        "MarkReturning" => "تسجيل عدم استلام العميل", "ReceiveReturn" => "تأكيد استلام الراجع",
        "Resume" => "استئناف محاولة التوصيل", "Redispatch" => "إعادة إرسال نفس الطلبية الراجعة",
        "CorrectDetails" => "تصحيح بيانات الشحنة", "CorrectState" => "تصحيح حالة سجلت بالخطأ", _ => action
    };
}
