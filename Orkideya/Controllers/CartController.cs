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
            if (quantity <= 0) // Ensure quantity is at least 1
            {
                return Json(new { success = false, message = "الكمية يجب أن تكون أكبر من صفر." });
            }

            var variant = await _context.ProductVariants
                                  .Include(v => v.Product)
                                  .FirstOrDefaultAsync(v => v.ProductVariantId == variantId);

            if (variant == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على خيار المنتج." });
            }

            List<CartItem> cart = GetCart();
            var cartItem = cart.FirstOrDefault(item => item.ProductVariantId == variantId);

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

        public IActionResult IncreaseQuantity(int id)
        {
            List<CartItem> cart = GetCart();
            var cartItem = cart.FirstOrDefault(item => item.ProductVariantId == id);

            if (cartItem != null)
            {
                cartItem.Quantity++;
                SaveCart(cart);
            }

            return RedirectToAction("Index");
        }

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
            return JsonSerializer.Deserialize<List<CartItem>>(cartJson);
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