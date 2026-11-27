namespace Orkideya.Models;

// Minimal display data: no customer name, phone, address, token or provider credentials.
public sealed record CheckoutReceipt(int OrderId, DateTime CreatedAtUtc, int Quantity,
    decimal ProductsTotal, decimal ShippingCost, decimal TotalAmount, string Status, string? PaymentMethod)
{
    /// <summary>Product lines of the order (name, size, quantity, unit price). Filled by the controller.</summary>
    public IReadOnlyList<CheckoutReceiptLine> Lines { get; init; } = [];
    /// <summary>Normal delivery fee when a free-delivery promotion applied (ShippingCost is then 0).</summary>
    public decimal? OriginalShippingCost { get; init; }

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


public sealed record CheckoutReceiptLine(string ProductName, string? Size, int Quantity, decimal UnitPrice)
{
    public decimal Subtotal => Quantity * UnitPrice;
}
