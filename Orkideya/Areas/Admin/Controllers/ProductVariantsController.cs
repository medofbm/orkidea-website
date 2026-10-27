using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ProductVariantsController : Controller
{
    private readonly ApplicationDbContext _context;
    public ProductVariantsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() =>
        View(await _context.ProductVariants.AsNoTracking().Include(v => v.Product).ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var variant = await _context.ProductVariants.AsNoTracking().Include(v => v.Product)
            .SingleOrDefaultAsync(v => v.ProductVariantId == id);
        return variant == null ? NotFound() : View(variant);
    }

    public IActionResult Create()
    {
        SetProducts(null);
        return View(new ProductVariantInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductVariantInput input)
    {
        var product = await _context.Products.FindAsync(input.ProductId);
        if (product == null) ModelState.AddModelError(nameof(input.ProductId), "المنتج غير موجود.");
        if (!ModelState.IsValid)
        {
            SetProducts(input.ProductId);
            return View(input);
        }
        _context.ProductVariants.Add(new ProductVariant
        {
            ProductId = product!.ProductId, Product = product,
            Size = input.Size.Trim(), Price = input.Price
        });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var variant = await _context.ProductVariants.FindAsync(id);
        if (variant == null) return NotFound();
        SetProducts(variant.ProductId);
        return View(new ProductVariantInput
        {
            ProductVariantId = variant.ProductVariantId, ProductId = variant.ProductId,
            Size = variant.Size, Price = variant.Price
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductVariantInput input)
    {
        if (id != input.ProductVariantId) return NotFound();
        var variant = await _context.ProductVariants.FindAsync(id);
        if (variant == null) return NotFound();
        var product = await _context.Products.FindAsync(input.ProductId);
        if (product == null) ModelState.AddModelError(nameof(input.ProductId), "المنتج غير موجود.");
        if (!ModelState.IsValid)
        {
            SetProducts(input.ProductId);
            return View(input);
        }
        variant.ProductId = product!.ProductId;
        variant.Product = product;
        variant.Size = input.Size.Trim();
        variant.Price = input.Price;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var variant = await _context.ProductVariants.AsNoTracking().Include(v => v.Product)
            .SingleOrDefaultAsync(v => v.ProductVariantId == id);
        return variant == null ? NotFound() : View(variant);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var variant = await _context.ProductVariants.FindAsync(id);
        if (variant == null) return NotFound();
        var productId = variant.ProductId;
        _context.ProductVariants.Remove(variant);
        await _context.SaveChangesAsync();
        return RedirectToAction("Edit", "Products", new { id = productId });
    }

    private void SetProducts(int? selected) =>
        ViewData["ProductId"] = new SelectList(_context.Products.AsNoTracking(), "ProductId", "Name", selected);
}
