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
    public class ProductVariantsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductVariantsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/ProductVariants
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.ProductVariants.Include(p => p.Product);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Admin/ProductVariants/Details/5
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

        // GET: Admin/ProductVariants/Create
        public IActionResult Create()
        {
            ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "Name");
            return View();
        }

        // POST: Admin/ProductVariants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductVariantId,ProductId,Size,Price")] ProductVariant productVariant)
        {
            if (ModelState.IsValid)
            {
                _context.Add(productVariant);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "Name", productVariant.ProductId);
            return View(productVariant);
        }

        // GET: Admin/ProductVariants/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productVariant = await _context.ProductVariants.FindAsync(id);
            if (productVariant == null)
            {
                return NotFound();
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "Name", productVariant.ProductId);
            return View(productVariant);
        }

        // POST: Admin/ProductVariants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductVariantId,ProductId,Size,Price")] ProductVariant productVariant)
        {
            if (id != productVariant.ProductVariantId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(productVariant);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductVariantExists(productVariant.ProductVariantId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "Name", productVariant.ProductId);
            return View(productVariant);
        }

        // GET: Admin/ProductVariants/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productVariant = await _context.ProductVariants
                .Include(p => p.Product)
                .FirstOrDefaultAsync(m => m.ProductVariantId == id);
            if (productVariant == null)
            {
                return NotFound();
            }

            return View(productVariant);
        }

        // POST: Admin/ProductVariants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var productVariant = await _context.ProductVariants.FindAsync(id);
            if (productVariant != null)
            {
                _context.ProductVariants.Remove(productVariant);
            }

            await _context.SaveChangesAsync();
            // Redirect back to the Edit page of the parent product
            return RedirectToAction("Edit", "Products", new { id = productVariant.ProductId });
        }

        private bool ProductVariantExists(int id)
        {
            return _context.ProductVariants.Any(e => e.ProductVariantId == id);
        }
    }
}