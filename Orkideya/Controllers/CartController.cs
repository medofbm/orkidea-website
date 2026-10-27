using Orkideya.Application.Cart;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using System.Text.Json;

namespace Orkideya.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            List<CartItem> cart = GetCart();
            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddToCartAjax(int variantId, int quantity = 1)
        {
            if (variantId <= 0 || !CartLimits.CanAdd(0, quantity))
            {
                return Json(new { success = false, message = "الكمية المطلوبة يجب أن تكون بين 1 و99." });
            }

            var variant = await _context.ProductVariants
                                  .Include(v => v.Product)
                                  .FirstOrDefaultAsync(v => v.ProductVariantId == variantId);

            if (variant == null || variant.Price <= 0)
            {
                return Json(new { success = false, message = "لم يتم العثور على خيار المنتج." });
            }

            List<CartItem> cart = GetCart();
            var cartItem = cart.FirstOrDefault(item => item.ProductVariantId == variantId);

            if (!CartLimits.CanAdd(cartItem?.Quantity ?? 0, quantity))
            {
                return Json(new { success = false, message = "الحد الأقصى لكل حجم هو 99 قطعة." });
            }

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductVariantId = variant.ProductVariantId,
                    ProductId = variant.ProductId,
                    ProductName = $"{variant.Product.Name} ({variant.Size})",
                    Price = variant.Price,
                    Quantity = quantity,
                    ImageUrl = variant.Product.ImageUrl
                });
            }
            SaveCart(cart);
            int newCartCount = cart.Sum(item => item.Quantity);
            return Json(new { success = true, count = newCartCount, message = "تمت الإضافة للسلة بنجاح!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromCart(int id)
        {
            List<CartItem> cart = GetCart();
            var cartItem = cart.FirstOrDefault(item => item.ProductVariantId == id);

            if (cartItem != null)
            {
                cart.Remove(cartItem);
                SaveCart(cart);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult IncreaseQuantity(int id)
        {
            List<CartItem> cart = GetCart();
            var cartItem = cart.FirstOrDefault(item => item.ProductVariantId == id);

            if (cartItem != null && CartLimits.CanAdd(cartItem.Quantity, 1))
            {
                cartItem.Quantity++;
                SaveCart(cart);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DecreaseQuantity(int id)
        {
            List<CartItem> cart = GetCart();
            var cartItem = cart.FirstOrDefault(item => item.ProductVariantId == id);

            if (cartItem != null)
            {
                if (cartItem.Quantity > 1)
                {
                    cartItem.Quantity--;
                }
                else
                {
                    cart.Remove(cartItem);
                }
                SaveCart(cart);
            }

            return RedirectToAction("Index");
        }

        private List<CartItem> GetCart()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
            {
                return new List<CartItem>();
            }
            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        private void SaveCart(List<CartItem> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString("Cart", cartJson);
        }

        [HttpGet]
        public JsonResult GetCount()
        {
            var cart = GetCart();
            int count = cart.Sum(i => i.Quantity);
            return Json(new { count });
        }
    }
}