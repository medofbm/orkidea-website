using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;

namespace Orkideya.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ExcelExportService _excelService;

        public OrdersController(ApplicationDbContext context, ExcelExportService excelService)
        {
            _context = context;
            _excelService = excelService;
        }

        /// <summary>
        /// Orders Index with filtering
        /// </summary>
        public async Task<IActionResult> Index(string status = "all", string search = "", int page = 1)
        {
            var query = _context.Orders.AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(o =>
                    o.CustomerName.Contains(search) ||
                    o.WhatsAppNumber.Contains(search) ||
                    o.City.Contains(search));
            }

            // Status filter
            if (status != "all")
            {
                query = query.Where(o => o.Status.ToLower() == status.ToLower());
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.SearchTerm = search;
            ViewBag.TotalOrders = orders.Count;

            // Calculate status counts
            ViewBag.AllCount = await _context.Orders.CountAsync();
            ViewBag.PendingCount = await _context.Orders.CountAsync(o => o.Status == "Pending");
            ViewBag.ProcessingCount = await _context.Orders.CountAsync(o => o.Status == "Processing");
            ViewBag.ShippedCount = await _context.Orders.CountAsync(o => o.Status == "Shipped");
            ViewBag.DeliveredCount = await _context.Orders.CountAsync(o => o.Status == "Delivered");
            ViewBag.CancelledCount = await _context.Orders.CountAsync(o => o.Status == "Cancelled");

            return View(orders);
        }

        /// <summary>
        /// Order Details
        /// </summary>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            // Get order items
            var orderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == id)
                .ToListAsync();

            // Load product names
            foreach (var item in orderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                ViewBag.ProductNames = ViewBag.ProductNames ?? new Dictionary<int, string>();
                ((Dictionary<int, string>)ViewBag.ProductNames)[item.ProductId] = product?.Name ?? "Unknown Product";
            }

            ViewBag.OrderItems = orderItems;

            return View(order);
        }

        /// <summary>
        /// Update Order Status (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return Json(new { success = false, message = "الطلب غير موجود" });
            }

            // Validate status
            var validStatuses = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };
            if (!validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "حالة غير صالحة" });
            }

            // Update status
            order.Status = validStatuses.First(value => value.Equals(status, StringComparison.OrdinalIgnoreCase));

            try
            {
                _context.Update(order);
                await _context.SaveChangesAsync();
                
                return Json(new { 
                    success = true, 
                    message = $"تم تحديث حالة الطلب #{order.OrderId} بنجاح",
                    newStatus = order.Status 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطأ: {ex.Message}" });
            }
        }

        /// <summary>
        /// Delete Order
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                // Delete order items first
                var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == id).ToListAsync();
                _context.OrderItems.RemoveRange(orderItems);

                // Delete order
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم حذف الطلب بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Export Orders to Excel (.xlsx) using ClosedXML
        /// </summary>
        public async Task<IActionResult> ExportToExcel(string status = "all", string search = "")
        {
            var query = _context.Orders.AsQueryable();

            // Apply same filters as Index
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(o =>
                    o.CustomerName.Contains(search) ||
                    o.WhatsAppNumber.Contains(search) ||
                    o.City.Contains(search));
            }

            if (status != "all")
            {
                query = query.Where(o => o.Status.ToLower() == status.ToLower());
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return File(_excelService.ExportOrders(orders),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Orkideya_Orders_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }
    }
}
