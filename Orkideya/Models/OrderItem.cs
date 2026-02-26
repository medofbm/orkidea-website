using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        /// <summary>حجم العبوة المطلوبة (مثلاً: 60ml, 120ml)</summary>
        public string? VariantSize { get; set; }
    }
}