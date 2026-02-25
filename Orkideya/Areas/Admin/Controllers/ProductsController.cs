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

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
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
            return View();
        }

        // POST: Admin/Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,Name,Description,ImageUrl,CategoryId")] Product product, IFormFile? imageFile)
        {
            // Handle image upload
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                Directory.CreateDirectory(uploadsFolder); // Ensure folder exists
                
                var uniqueFileName = $"{Guid.NewGuid()}_{imageFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }
                
                product.ImageUrl = $"/images/products/{uniqueFileName}";
            }
            
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المنتج بنجاح";
                return RedirectToAction(nameof(Edit), new { id = product.ProductId });
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
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
            return View(product);
        }

        // POST: Admin/Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,Name,Description,ImageUrl,CategoryId")] Product product, IFormFile? imageFile)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }
            
            // Get existing product from database to preserve ImageUrl if no new upload
            var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
            if (existingProduct == null)
            {
                return NotFound();
            }
            
            // AGGRESSIVELY remove ALL navigation property validations
            // These often fail incorrectly with EF Core tracking
            ModelState.Remove("ImageUrl");
            ModelState.Remove("imageFile");
            ModelState.Remove("Category");
            ModelState.Remove("ProductVariants");
            ModelState.Remove("Product");
            
            // Remove any other potential blockers
            foreach (var key in ModelState.Keys.Where(k => k.Contains("Category") || k.Contains("Variant")).ToList())
            {
                ModelState.Remove(key);
            }
            
            // Handle image upload
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                Directory.CreateDirectory(uploadsFolder); // Ensure folder exists
                
                var uniqueFileName = $"{Guid.NewGuid()}_{imageFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }
                
                product.ImageUrl = $"/images/products/{uniqueFileName}";
            }
            else if (string.IsNullOrEmpty(product.ImageUrl))
            {
                // No new image uploaded and no URL provided - keep existing image
                product.ImageUrl = existingProduct.ImageUrl;
            }
            // If product.ImageUrl has a value (from form), it means user entered a new URL - use it

            if (!ModelState.IsValid)
            {
                // Debug: Log validation errors
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    Console.WriteLine($"ModelState Error: {error.ErrorMessage}");
                }
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["SuccessMessage"] = "تم تحديث المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            
            ViewBag.Variants = await _context.ProductVariants.Where(v => v.ProductId == id).ToListAsync();
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVariant(ProductVariant variant)
        {
            ModelState.Remove("Product"); 

            if (ModelState.IsValid)
            {
                _context.ProductVariants.Add(variant);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = variant.ProductId });
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