using System.Security.Claims;
using Orkideya.Application.Orders;
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
        private readonly OrderStatusService _statusService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(ApplicationDbContext context, ExcelExportService excelService, OrderStatusService statusService, ILogger<OrdersController> logger)
        {
            _context = context;
            _excelService = excelService;
            _statusService = statusService;
            _logger = logger;
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
            var history = await _context.OrderStatusChanges.AsNoTracking()
                .Where(change => change.OrderId == id).OrderBy(change => change.Revision).ToListAsync();
            ViewBag.StatusHistory = history;
            var actorIds = history.Select(change => change.ActorUserId).Distinct().ToArray();
            ViewBag.StatusActors = await _context.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.UserName ?? "حساب إداري");

            return View(order);
        }

        /// <summary>
        /// Update Order Status (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string? status, int? expectedRevision)
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Forbid();
            try
            {
                var result = await _statusService.ChangeAsync(id, status, expectedRevision, actor, HttpContext.RequestAborted);
                return result switch
                {
                    StatusChangeResult.Changed or StatusChangeResult.Unchanged => Json(new {
                        success = true, message = $"تم حفظ حالة الطلب #{id}", newStatus = status }),
                    StatusChangeResult.Conflict => Conflict(new { success = false,
                        message = "تغيّر الطلب من صفحة أخرى. حدّث الصفحة قبل إعادة المحاولة." }),
                    StatusChangeResult.Missing => NotFound(new { success = false, message = "الطلب غير موجود" }),
                    _ => BadRequest(new { success = false, message = "بيانات التحديث غير صالحة. حدّث الصفحة وأعد المحاولة." })
                };
            }
            catch (DbUpdateException)
            {
                _logger.LogError("Order status save failed for order {OrderId}", id);
                return StatusCode(500, new { success = false, message = "تعذر حفظ الحالة. حدّث الصفحة للتحقق قبل إعادة المحاولة." });
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
                if (await _context.OrderStatusChanges.AnyAsync(change => change.OrderId == id))
                {
                    TempData["OrderError"] = "هذا الطلب لديه سجل تغييرات محفوظ؛ استخدم الإلغاء بدلاً من حذف تاريخه.";
                    return RedirectToAction(nameof(Index));
                }
                // Delete order items first
                var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == id).ToListAsync();
                _context.OrderItems.RemoveRange(orderItems);

                // Delete order
                _context.Orders.Remove(order);
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateException)
                {
                    TempData["OrderError"] = "تعذر حذف الطلب؛ قد يكون تغيّر من صفحة أخرى. حدّث الصفحة.";
                    return RedirectToAction(nameof(Index));
                }

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
