namespace Orkideya.Application.Cart;

/// <summary>Technical abuse limit, not a stock availability promise.</summary>
public static class CartLimits
{
    public const int MaxQuantityPerVariant = 99;
    public static bool CanAdd(int currentQuantity, int quantity) =>
        currentQuantity >= 0 && currentQuantity <= MaxQuantityPerVariant &&
        quantity > 0 && quantity <= MaxQuantityPerVariant - currentQuantity;
}
