namespace Orkideya.Models;

// Minimal display data: no customer name, phone, address, token or provider credentials.
public sealed record CheckoutReceipt(int OrderId, DateTime CreatedAtUtc, int Quantity,
    decimal ProductsTotal, decimal ShippingCost, decimal TotalAmount, string Status, string? PaymentMethod)
{
    public string StatusLabel => Status switch
    {
        "Pending" => "بانتظار مراجعة التوفر والتجهيز",
        "Processing" => "قيد التجهيز",
        "Shipped" => "قيد التوصيل",
        "Delivered" => "تم التسليم",
        "Cancelled" => "ملغي",
        _ => "تحتاج الحالة إلى مراجعة"
    };
}
