using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Cart;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;
using System.Text.Json;

namespace Orkideya.Controllers;

public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly WhatsAppNotificationService _whatsAppService;
    public CheckoutController(ApplicationDbContext context, WhatsAppNotificationService whatsAppService)
    {
        _context = context;
        _whatsAppService = whatsAppService;
    }

    public async Task<IActionResult> Index()
    {
        var cart = GetCart();
        if (!IsValidCart(cart)) return InvalidCart();
        var variants = await LoadVariants(cart);
        if (!RefreshPrices(cart, variants, out _)) return InvalidCart();
        HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));
        return View(await BuildViewModel(cart, new CheckoutInput()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder([Bind(Prefix = "Input")] CheckoutInput input)
    {
        var cart = GetCart();
        if (!IsValidCart(cart)) return InvalidCart();
        var variants = await LoadVariants(cart);
        if (!RefreshPrices(cart, variants, out var priceChanged)) return InvalidCart();
        // Update the session before returning a price-change warning so the user can
        // review the new total and explicitly submit again.
        HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));
        if (priceChanged)
            ModelState.AddModelError("", "تغير سعر أحد المنتجات. راجع الإجمالي الجديد ثم أكد الطلب مرة أخرى.");

        var rate = await _context.ShippingRates.AsNoTracking()
            .SingleOrDefaultAsync(rate => rate.ShippingRateId == input.ShippingRateId);
        if (rate == null || rate.Price < 0 || string.IsNullOrWhiteSpace(rate.CityName))
            ModelState.AddModelError("Input.ShippingRateId", "مدينة التوصيل غير متاحة. اختر مدينة من القائمة.");
        if (!ModelState.IsValid)
            return View("Index", await BuildViewModel(cart, input));

        var order = new Order
        {
            CustomerName = $"{input.FirstName.Trim()} {input.LastName.Trim()}",
            WhatsAppNumber = input.WhatsAppNumber.Trim(),
            ShippingAddress = input.ShippingAddress.Trim(),
            City = rate!.CityName,
            ShippingCost = rate.Price,
            TotalAmount = cart.Sum(item => item.SubTotal) + rate.Price,
            OrderDate = DateTime.UtcNow,
            PaymentMethod = "الدفع عند الاستلام",
            Status = "Pending"
        };

        // The existing two-save persistence flow is retained until transaction and
        // retry/idempotency behavior can be verified against an isolated SQL Server.
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        foreach (var item in cart)
            _context.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = variants[item.ProductVariantId].ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.Price,
                VariantSize = variants[item.ProductVariantId].Size
            });
        await _context.SaveChangesAsync();
        HttpContext.Session.Remove("Cart");
        await _whatsAppService.SendNotification(order);
        return RedirectToAction(nameof(Confirmation));
    }

    public IActionResult Confirmation() => View();

    private async Task<CheckoutViewModel> BuildViewModel(List<CartItem> cart, CheckoutInput input) => new()
    {
        CartItems = cart,
        Input = input,
        CartTotal = cart.Sum(item => item.SubTotal),
        ShippingRates = await _context.ShippingRates.AsNoTracking()
            .Where(rate => rate.Price >= 0 && rate.CityName != "")
            .OrderBy(rate => rate.CityName).ToListAsync()
    };

    private Task<Dictionary<int, ProductVariant>> LoadVariants(List<CartItem> cart)
    {
        var ids = cart.Select(item => item.ProductVariantId).ToArray();
        return _context.ProductVariants.AsNoTracking().Include(variant => variant.Product)
            .Where(variant => ids.Contains(variant.ProductVariantId))
            .ToDictionaryAsync(variant => variant.ProductVariantId);
    }

    private static bool RefreshPrices(List<CartItem> cart, Dictionary<int, ProductVariant> variants, out bool changed)
    {
        changed = false;
        if (cart.Any(item => !variants.TryGetValue(item.ProductVariantId, out var variant) ||
            variant.Product == null || variant.Price <= 0)) return false;
        foreach (var item in cart)
        {
            var variant = variants[item.ProductVariantId];
            changed |= item.Price != variant.Price;
            item.Price = variant.Price;
            item.ProductId = variant.ProductId;
            item.ProductName = $"{variant.Product.Name} ({variant.Size})";
            item.ImageUrl = variant.Product.ImageUrl;
        }
        return true;
    }

    private static bool IsValidCart(List<CartItem> cart) => cart.Count is > 0 and <= 100 &&
        cart.All(item => item != null && item.ProductVariantId > 0 && CartLimits.CanAdd(0, item.Quantity)) &&
        cart.Select(item => item.ProductVariantId).Distinct().Count() == cart.Count;

    private IActionResult InvalidCart()
    {
        TempData["CartError"] = "راجع السلة: قد يكون أحد الأحجام غير متاح أو الكمية غير صالحة.";
        return RedirectToAction("Index", "Cart");
    }

    private List<CartItem> GetCart()
    {
        var json = HttpContext.Session.GetString("Cart");
        if (string.IsNullOrEmpty(json)) return new();
        try { return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
