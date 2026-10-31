using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class ShippingRate
    {
        [Key]
        public int ShippingRateId { get; set; }
        public required string CityName { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public string Region { get; set; } = "المنطقة الغربية";
        public string DeliveryDuration { get; set; } = "24-48 ساعة";
    }
}
