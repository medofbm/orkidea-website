using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        [ConcurrencyCheck]
        public int StatusRevision { get; set; }
        [MaxLength(32)] public string StockState { get; set; } = "Untracked";
        public Guid? CheckoutToken { get; set; }
        [MaxLength(64)]
        public string? CheckoutRequestHash { get; set; }
        public OrderCreation? Creation { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public List<OrderNotification> Notifications { get; set; } = new();
        public required string CustomerName { get; set; }
        public required string WhatsAppNumber { get; set; }
        public required string ShippingAddress { get; set; }
        public required string City { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime OrderDate { get; set; }
        
        // Order Status: Pending, Processing, Shipped, Delivered, Cancelled
        [Display(Name = "حالة الطلب")]
        public string Status { get; set; } = "Pending";
    }
}
