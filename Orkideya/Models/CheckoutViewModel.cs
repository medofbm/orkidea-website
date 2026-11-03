namespace Orkideya.Models
{
    public class CheckoutViewModel
    {
        public required List<CartItem> CartItems { get; set; }
        public required List<ShippingRate> ShippingRates { get; set; }
        public CheckoutInput Input { get; set; } = new();
        public IReadOnlyDictionary<string, Orkideya.Application.Shipping.QuoteDestination>? HomeDestinations { get; set; }
        public decimal? QuotedFee { get; set; }
        public decimal CartTotal { get; set; }
    }
}