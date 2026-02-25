namespace Orkideya.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public int ProductVariantId { get; set; } // الأهم: معرّف الحجم والسعر
        public required string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal SubTotal => Price * Quantity;
        public string? ImageUrl { get; set; }
    }
}