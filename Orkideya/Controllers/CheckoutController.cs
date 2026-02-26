using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;
using System.Text.Json;

namespace Orkideya.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ExcelExportService _excelService;
        private readonly WhatsAppNotificationService _whatsAppService;

        public CheckoutController(ApplicationDbContext context, ExcelExportService excelService, WhatsAppNotificationService whatsAppService)
        {
            _context = context;
            _excelService = excelService;
            _whatsAppService = whatsAppService;
        }

        // --- تم تعديل هذه الدالة ---
        public async Task<IActionResult> Index()
        {
            var cart = GetCart();
            if (cart == null || cart.Count == 0)
            {
                // إذا كانت السلة فارغة، لا تذهب لصفحة الدفع
                return RedirectToAction("Index", "Cart");
            }

            var viewModel = new CheckoutViewModel
            {
                CartItems = cart,
                ShippingRates = await _context.ShippingRates.ToListAsync(),
                CartTotal = cart.Sum(item => item.SubTotal)
            };
            return View(viewModel);
        }

        // --- تم تعديل هذه الدالة ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(string whatsAppNumber, string firstName, string lastName, string shippingAddress, int shippingRateId, string otherCity)
        {
            var cart = GetCart();
            if (cart == null || cart.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // ── التحقق من البيانات قبل الحفظ ──────────────────────────────
            bool hasErrors = false;

            if (string.IsNullOrWhiteSpace(whatsAppNumber))
            {
                ModelState.AddModelError("whatsAppNumber", "الرجاء إدخال رقم الواتساب");
                hasErrors = true;
            }
            if (string.IsNullOrWhiteSpace(firstName))
            {
                ModelState.AddModelError("firstName", "الرجاء إدخال الاسم الأول");
                hasErrors = true;
            }
            if (string.IsNullOrWhiteSpace(lastName))
            {
                ModelState.AddModelError("lastName", "الرجاء إدخال اسم العائلة");
                hasErrors = true;
            }
            if (string.IsNullOrWhiteSpace(shippingAddress))
            {
                ModelState.AddModelError("shippingAddress", "الرجاء إدخال عنوان الشحن");
                hasErrors = true;
            }
            if (shippingRateId == 0 && string.IsNullOrWhiteSpace(otherCity))
            {
                ModelState.AddModelError("shippingRateId", "الرجاء اختيار مدينة التوصيل أو كتابة اسمها");
                hasErrors = true;
            }

            if (hasErrors)
            {
                var errorViewModel = new CheckoutViewModel
                {
                    CartItems = cart,
                    ShippingRates = await _context.ShippingRates.ToListAsync(),
                    CartTotal = cart.Sum(item => item.SubTotal)
                };
                return View("Index", errorViewModel);
            }
            // ──────────────────────────────────────────────────────────────

            string customerName = $"{firstName} {lastName}";

            decimal shippingCost = 0;
            string cityName = "";

            if (shippingRateId > 0)
            {
                var rate = await _context.ShippingRates.FindAsync(shippingRateId);
                if (rate != null)
                {
                    shippingCost = rate.Price;
                    cityName = rate.CityName;
                }
            }
            else
            {
                shippingCost = 0;
                cityName = otherCity;
            }

            decimal itemsTotal = cart.Sum(item => item.SubTotal);
            decimal totalAmount = itemsTotal + shippingCost;

            var order = new Order
            {
                CustomerName = customerName,
                WhatsAppNumber = whatsAppNumber,
                ShippingAddress = shippingAddress,
                City = cityName,
                ShippingCost = shippingCost,
                TotalAmount = totalAmount,
                OrderDate = DateTime.Now,
                PaymentMethod = "الدفع عند الاستلام",
                Status = "Pending"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in cart)
            {
                // جلب حجم العبوة من قاعدة البيانات عبر معرّف الحجم
                var variantSize = await _context.ProductVariants
                    .Where(v => v.ProductVariantId == item.ProductVariantId)
                    .Select(v => v.Size)
                    .FirstOrDefaultAsync();

                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price,
                    VariantSize = variantSize ?? "-"
                };
                _context.OrderItems.Add(orderItem);
            }
            await _context.SaveChangesAsync();

            try
            {
                _excelService.AddOrderToSheet(order, cart);
            }
            catch (IOException) { /* تجاهل الخطأ إذا كان الملف مقفلاً */ }

            await _whatsAppService.SendNotification(order);

            HttpContext.Session.Remove("Cart");

            return RedirectToAction("Confirmation");
        }

        public IActionResult Confirmation()
        {
            return View();
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
    }
}