using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Areas.Admin.Models;

namespace Orkideya.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Admin Dashboard - Command Center
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel
            {
                // Calculate key statistics
                TotalSales = await CalculateTotalSalesAsync(),
                PendingOrdersCount = await GetPendingOrdersCountAsync(),
                LowStockProductsCount = await GetLowStockProductsCountAsync(),
                TotalUsersCount = await GetTotalUsersCountAsync(),
                
                // Sales trend calculation
                SalesChangePercentage = await CalculateSalesChangeAsync(),
                
                // Chart data - Last 30 days sales
                SalesChartData = await GetSalesChartDataAsync(),
                
                // Categories distribution
                CategoriesData = await GetCategoriesDistributionAsync(),
                
                // Recent orders for activity feed
                RecentOrders = await GetRecentOrdersAsync(5)
            };

            return View(viewModel);
        }

        #region Analytics Methods

        /// <summary>
        /// Calculate total sales revenue
        /// </summary>
        private async Task<decimal> CalculateTotalSalesAsync()
        {
            return await _context.Orders
                .Where(o => o.OrderDate >= DateTime.Now.AddMonths(-1))
                .SumAsync(o => o.TotalAmount);
        }

        /// <summary>
        /// Get count of pending orders
        /// </summary>
        private async Task<int> GetPendingOrdersCountAsync()
        {
            // Since Order model doesn't have Status yet, we'll return total count
            // After we add Status field in Orders controller, this will filter by status
            return await _context.Orders
                .Where(o => o.OrderDate >= DateTime.Now.AddDays(-7))
                .CountAsync();
        }

        /// <summary>
        /// Get count of low stock products
        /// Note: Product model doesn't have Stock field yet
        /// This is a placeholder for future implementation
        /// </summary>
        private async Task<int> GetLowStockProductsCountAsync()
        {
            // منتجات بدون أي حجم/سعر محدد - هذه هي المشكلة الحقيقية
            var productsWithNoVariants = await _context.Products
                .Include(p => p.ProductVariants)
                .Where(p => !p.ProductVariants.Any())
                .CountAsync();
            
            return productsWithNoVariants;
        }

        /// <summary>
        /// Get total registered users count
        /// </summary>
        private async Task<int> GetTotalUsersCountAsync()
        {
            return await _userManager.Users.CountAsync();
        }

        /// <summary>
        /// Calculate sales change percentage (last 7 days vs previous 7 days)
        /// </summary>
        private async Task<decimal> CalculateSalesChangeAsync()
        {
            var last7DaysSales = await _context.Orders
                .Where(o => o.OrderDate >= DateTime.Now.AddDays(-7))
                .SumAsync(o => o.TotalAmount);

            var previous7DaysSales = await _context.Orders
                .Where(o => o.OrderDate >= DateTime.Now.AddDays(-14) && o.OrderDate < DateTime.Now.AddDays(-7))
                .SumAsync(o => o.TotalAmount);

            if (previous7DaysSales == 0) return 0;

            return ((last7DaysSales - previous7DaysSales) / previous7DaysSales) * 100;
        }

        /// <summary>
        /// Get sales data for the last 30 days (for line chart)
        /// </summary>
        private async Task<SalesChartDataViewModel> GetSalesChartDataAsync()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30).Date;
            
            var salesData = await _context.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Fill in missing dates with zero sales
            var allDates = Enumerable.Range(0, 30)
                .Select(i => DateTime.Now.AddDays(-29 + i).Date)
                .ToList();

            var chartData = new SalesChartDataViewModel
            {
                Labels = allDates.Select(d => d.ToString("dd/MM")).ToList(),
                Data = allDates.Select(d =>
                {
                    var dayData = salesData.FirstOrDefault(s => s.Date == d);
                    return dayData?.Total ?? 0;
                }).ToList()
            };

            return chartData;
        }

        /// <summary>
        /// Get categories distribution for doughnut chart
        /// </summary>
        private async Task<CategoriesDataViewModel> GetCategoriesDistributionAsync()
        {
            var categories = await _context.Categories
                .Select(c => new
                {
                    c.Name,
                    ProductCount = _context.Products.Count(p => p.CategoryId == c.CategoryId)
                })
                .Where(c => c.ProductCount > 0)
                .ToListAsync();

            return new CategoriesDataViewModel
            {
                Labels = categories.Select(c => c.Name).ToList(),
                Data = categories.Select(c => c.ProductCount).ToList()
            };
        }

        /// <summary>
        /// Get recent orders for activity feed
        /// </summary>
        private async Task<List<Order>> GetRecentOrdersAsync(int count)
        {
            return await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// AJAX endpoint to get sales chart data for different time ranges
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetSalesChartData(int days = 30)
        {
            var startDate = DateTime.Now.AddDays(-days).Date;
            
            var salesData = await _context.Orders
                .Where(o => o.OrderDate >= startDate)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Fill in missing dates with zero sales
            var allDates = Enumerable.Range(0, days)
                .Select(i => DateTime.Now.AddDays(-(days - 1) + i).Date)
                .ToList();

            var chartData = new SalesChartDataViewModel
            {
                Labels = allDates.Select(d => d.ToString("dd/MM")).ToList(),
                Data = allDates.Select(d =>
                {
                    var dayData = salesData.FirstOrDefault(s => s.Date == d);
                    return dayData?.Total ?? 0;
                }).ToList()
            };

            return Json(chartData);
        }

        #endregion
    }
}
