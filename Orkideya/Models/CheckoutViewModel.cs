namespace Orkideya.Models
{
    public class CheckoutViewModel
    {
        public required List<CartItem> CartItems { get; set; }
        public required List<ShippingRate> ShippingRates { get; set; }
        public decimal CartTotal { get; set; }
    }
}