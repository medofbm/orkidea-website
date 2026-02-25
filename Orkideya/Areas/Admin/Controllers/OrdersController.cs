using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;
using ClosedXML.Excel;

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
            order.Status = status;

            try
            {
                _context.Update(order);
                await _context.SaveChangesAsync();
                
                return Json(new { 
                    success = true, 
                    message = $"تم تحديث حالة الطلب #{order.OrderId} بنجاح",
                    newStatus = status 
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

            // Create Excel workbook using ClosedXML
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("الطلبات");
                
                // Set RTL for Arabic
                worksheet.RightToLeft = true;
                
                // Headers
                var headers = new[] {
                    "رقم الطلب", "اسم العميل", "رقم الواتساب", "المدينة", 
                    "العنوان", "تاريخ الطلب", "المبلغ الإجمالي", "تكلفة الشحن", 
                    "الحالة", "طريقة الدفع"
                };
                
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 12;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(106, 27, 154); // Purple
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                
                // Data rows
                int row = 2;
                foreach (var order in orders)
                {
                    worksheet.Cell(row, 1).Value = order.OrderId;
                    worksheet.Cell(row, 2).Value = order.CustomerName;
                    worksheet.Cell(row, 3).Value = order.WhatsAppNumber;
                    worksheet.Cell(row, 4).Value = order.City;
                    worksheet.Cell(row, 5).Value = order.ShippingAddress;
                    worksheet.Cell(row, 6).Value = order.OrderDate.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 7).Value = order.TotalAmount;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(row, 8).Value = order.ShippingCost;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(row, 9).Value = GetStatusArabic(order.Status);
                    worksheet.Cell(row, 10).Value = order.PaymentMethod ?? "";
                    
                    row++;
                }
                
                // Auto-fit columns
                worksheet.Columns().AdjustToContents();
                
                // Add borders
                var dataRange = worksheet.Range(1, 1, row - 1, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                
                // Generate file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var fileName = $"Orkideya_Orders_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    
                    return File(stream.ToArray(), 
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                        fileName);
                }
            }
        }
        
        /// <summary>
        /// Get Arabic status name
        /// </summary>
        private string GetStatusArabic(string status)
        {
            return status?.ToLower() switch
            {
                "pending" => "جديد",
                "processing" => "قيد المعالجة",
                "shipped" => "تم الشحن",
                "delivered" => "مكتمل",
                "cancelled" => "ملغي",
                _ => "جديد"
            };
        }
    }
}
