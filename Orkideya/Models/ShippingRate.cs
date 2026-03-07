using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models
{
    public class ShippingRate
    {
        [Key]
        public int ShippingRateId { get; set; }
        public required string CityName { get; set; }
        public decimal Price { get; set; }
    }
}
