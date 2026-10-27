using Orkideya.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly ProductImageStorage _images;

        public ProductsController(ApplicationDbContext context, ProductImageStorage images)
        {
            _context = context;
            _images = images;
        }

        // GET: Admin/Products
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants); // Eager load variants for accurate count
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Admin/Products/Details/5
        
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants) // <-- هذا السطر مهم لجلب الأحجام
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Admin/Products/Create
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name");
            return View(new ProductInput());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        public async Task<IActionResult> Create(ProductInput input, IFormFile? imageFile)
        {
            await ValidateInputAsync(input);
            if (ModelState.IsValid && await TrySaveImageAsync(input, imageFile))
            {
                var product = new Product
                {
                    Name = input.Name.Trim(), Description = input.Description,
                    CategoryId = input.CategoryId, ImageUrl = input.ImageUrl
                };
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المنتج بنجاح";
                return RedirectToAction(nameof(Edit), new { id = product.ProductId });
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", input.CategoryId);
            return View(input);
        }

        // GET: Admin/Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Variants = await _context.ProductVariants.Where(v => v.ProductId == id).ToListAsync();

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(new ProductInput
            {
                ProductId = product.ProductId, Name = product.Name,
                Description = product.Description, CategoryId = product.CategoryId, ImageUrl = product.ImageUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, ProductInput input, IFormFile? imageFile)
        {
            if (id != input.ProductId) return NotFound();
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            await ValidateInputAsync(input);
            if (ModelState.IsValid && await TrySaveImageAsync(input, imageFile))
            {
                product.Name = input.Name.Trim();
                product.Description = input.Description;
                product.CategoryId = input.CategoryId;
                product.ImageUrl = input.ImageUrl;
                await _context.SaveChangesAsync();
                // Keep old images until a separate reference-aware cleanup is implemented.
                TempData["SuccessMessage"] = "تم تحديث المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Variants = await _context.ProductVariants.Where(v => v.ProductId == id).ToListAsync();
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", input.CategoryId);
            return View(input);
        }

        private async Task ValidateInputAsync(ProductInput input)
        {
            if (!ProductImageStorage.IsAllowedReference(input.ImageUrl))
                ModelState.AddModelError(nameof(input.ImageUrl), "استخدم رابط HTTPS أو صورة محلية ضمن /images/.");
            if (input.CategoryId.HasValue && !await _context.Categories.AnyAsync(c => c.CategoryId == input.CategoryId.Value))
                ModelState.AddModelError(nameof(input.CategoryId), "الفئة المختارة غير موجودة.");
        }

        private async Task<bool> TrySaveImageAsync(ProductInput input, IFormFile? imageFile)
        {
            if (imageFile == null) return true;
            try
            {
                input.ImageUrl = await _images.SaveAsync(imageFile, HttpContext.RequestAborted);
                return true;
            }
            catch (InvalidDataException exception)
            {
                ModelState.AddModelError("imageFile", exception.Message);
                return false;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVariant(ProductVariantInput input)
        {
            var product = await _context.Products.FindAsync(input.ProductId);
            if (product == null) return NotFound();
            if (!ModelState.IsValid)
            {
                TempData["VariantError"] = "تعذر إضافة الحجم. أدخل حجماً حتى 50 حرفاً وسعراً موجباً بدقة رقمين عشريين.";
                return RedirectToAction(nameof(Edit), new { id = product.ProductId });
            }
            _context.ProductVariants.Add(new ProductVariant
            {
                ProductId = product.ProductId, Product = product,
                Size = input.Size.Trim(), Price = input.Price
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = product.ProductId });
        }

        // GET: Admin/Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Admin/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                var variants = _context.ProductVariants.Where(v => v.ProductId == id);
                _context.ProductVariants.RemoveRange(variants);

                _context.Products.Remove(product);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }
    }
}