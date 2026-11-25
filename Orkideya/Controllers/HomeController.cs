using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using System.Diagnostics;

namespace Orkideya.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _context.Categories.Where(c => !c.IsArchived).ToListAsync();
            ViewBag.Reviews = await _context.Reviews.Where(r => r.IsVisible).ToListAsync();

            var allProducts = await _context.Products.Where(p => !p.IsArchived)
                                         .Include(p => p.ProductVariants.Where(v => !v.IsArchived))
                                         .ToListAsync();

            ViewBag.FeaturedProducts = allProducts.Where(p => p.ProductVariants.Any()).Take(4).ToList();

            return View(allProducts);
        }

        public async Task<IActionResult> ProductsByCategory(int categoryId)
        {
            ViewBag.Categories = await _context.Categories.Where(c => !c.IsArchived).ToListAsync();
            var products = await _context.Products
                                         .Include(p => p.ProductVariants.Where(v => !v.IsArchived))
                                         .Where(p => p.CategoryId == categoryId && !p.IsArchived)
                                         .ToListAsync();
            ViewBag.CurrentCategoryName = _context.Categories.FirstOrDefault(c => c.CategoryId == categoryId)?.Name;
            return View("Index", products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("public-forms")]
        public async Task<IActionResult> SendMessage(string? name, string? email, string? message, string? phoneNumber)
        {
            // Same validation as the Vue contact API (length limits, email and phone format).
            var error = ContactApiController.Validate(
                new ContactApiController.ContactInput(name, phoneNumber, email, message),
                out var cleanName, out var cleanPhone, out var cleanEmail, out var cleanMessage);
            if (error is not null)
            {
                TempData["MessageError"] = error;
                return RedirectToAction("Index");
            }

            _context.ContactMessages.Add(new ContactMessage
            {
                Name = cleanName,
                Email = cleanEmail,
                Message = cleanMessage,
                PhoneNumber = cleanPhone,
                ReceivedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["MessageSuccess"] = "شكراً لك! لقد تم استلام رسالتك وسنتواصل معك قريباً.";
            return RedirectToAction("Index");
        }
        // -------------------------

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> ProductDetail(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductVariants.Where(v => !v.IsArchived))
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id && !p.IsArchived);

            if (product == null) return NotFound();

            // عدد مرات طلب هذا المنتج
            var orderCount = await _context.OrderItems
                .Where(oi => oi.ProductId == id)
                .SumAsync(oi => (int?)oi.Quantity) ?? 0;

            // منتجات مشابهة (نفس الفئة)
            var related = await _context.Products
                .Include(p => p.ProductVariants.Where(v => !v.IsArchived))
                .Where(p => p.CategoryId == product.CategoryId && p.ProductId != id && !p.IsArchived && p.ProductVariants.Any(v => !v.IsArchived))
                .Take(4)
                .ToListAsync();

            ViewBag.OrderCount = orderCount;
            ViewBag.RelatedProducts = related;
            ViewBag.Categories = await _context.Categories.Where(c => !c.IsArchived).ToListAsync();

            return View(product);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}