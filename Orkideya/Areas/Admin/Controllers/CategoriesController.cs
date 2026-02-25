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
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Categories
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories.ToListAsync();
            
            // Calculate product count for each category
            var productCounts = new Dictionary<int, int>();
            foreach (var category in categories)
            {
                productCounts[category.CategoryId] = await _context.Products
                    .CountAsync(p => p.CategoryId == category.CategoryId);
            }
            ViewBag.ProductCounts = productCounts;
            
            return View(categories);
        }

        // GET: Admin/Categories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.CategoryId == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Admin/Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CategoryId,Name")] Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }
        
        // POST: Admin/Categories/CreateAjax (for modal)
        [HttpPost]
        public async Task<IActionResult> CreateAjax(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "اسم الفئة مطلوب" });
            }
            
            try
            {
                var category = new Category { Name = name };
                _context.Add(category);
                await _context.SaveChangesAsync();
                
                return Json(new { 
                    success = true, 
                    message = "تمت إضافة الفئة بنجاح",
                    category = new { 
                        categoryId = category.CategoryId, 
                        name = category.Name 
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطأ: {ex.Message}" });
            }
        }

        // GET: Admin/Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Admin/Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CategoryId,Name")] Category category)
        {
            if (id != category.CategoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.CategoryId))
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
            return View(category);
        }
        
        // POST: Admin/Categories/EditAjax (for modal)
        [HttpPost]
        public async Task<IActionResult> EditAjax(int id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "اسم الفئة مطلوب" });
            }
            
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return Json(new { success = false, message = "الفئة غير موجودة" });
                }
                
                category.Name = name;
                _context.Update(category);
                await _context.SaveChangesAsync();
                
                return Json(new { 
                    success = true, 
                    message = "تم تحديث الفئة بنجاح",
                    category = new { 
                        categoryId = category.CategoryId, 
                        name = category.Name 
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطأ: {ex.Message}" });
            }
        }
        
        // GET: Admin/Categories/GetCategory/5 (for modal editing)
        [HttpGet]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return Json(new { success = false, message = "الفئة غير موجودة" });
            }
            
            return Json(new { 
                success = true, 
                category = new { 
                    categoryId = category.CategoryId, 
                    name = category.Name 
                }
            });
        }

        // GET: Admin/Categories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.CategoryId == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CategoryId == id);
        }
    }
}