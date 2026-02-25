using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public required string CustomerName { get; set; }
        public required string WhatsAppNumber { get; set; }
        public required string ShippingAddress { get; set; }
        public required string City { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime OrderDate { get; set; }
        
        // Order Status: Pending, Processing, Shipped, Delivered, Cancelled
        [Display(Name = "حالة الطلب")]
        public string Status { get; set; } = "Pending";
    }
}