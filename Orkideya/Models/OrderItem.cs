using System.ComponentModel.DataAnnotations.Schema;
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
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>حجم العبوة المطلوبة (مثلاً: 60ml, 120ml)</summary>
        public string? VariantSize { get; set; }
        public int? StockAccountId { get; set; }
        [Column(TypeName = "decimal(18,3)")] public decimal? StockUnitsPerItem { get; set; }
        [MaxLength(8)] public string? StockUnit { get; set; }
    }
}