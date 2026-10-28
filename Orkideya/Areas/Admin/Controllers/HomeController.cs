using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Areas.Admin.Models;
using Orkideya.Application.Orders;

namespace Orkideya.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class HomeController(ApplicationDbContext context, UserManager<IdentityUser> userManager,
    TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index()
    {
        var metrics = new DashboardMetrics(context, clock.GetUtcNow().UtcDateTime);
        var categories = await context.Categories.Select(c => new
        {
            c.Name, Count = context.Products.Count(p => p.CategoryId == c.CategoryId)
        }).Where(c => c.Count > 0).ToListAsync();
        return View(new DashboardViewModel
        {
            TotalSales = await metrics.ThirtyDaysAsync(),
            PendingOrdersCount = await metrics.PendingAsync(),
            LowStockProductsCount = await context.Products.CountAsync(p => !p.ProductVariants.Any()),
            TotalUsersCount = await userManager.Users.CountAsync(),
            SalesChangePercentage = await metrics.WeeklyChangeAsync(),
            SalesChartData = await metrics.ChartAsync(30),
            CategoriesData = new CategoriesDataViewModel
            {
                Labels = categories.Select(c => c.Name).ToList(),
                Data = categories.Select(c => c.Count).ToList()
            },
            RecentOrders = await context.Orders.AsNoTracking().OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId).Take(5).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSalesChartData(int days = 30)
    {
        if (days is not (7 or 30)) return BadRequest();
        return Json(await new DashboardMetrics(context, clock.GetUtcNow().UtcDateTime).ChartAsync(days));
    }
}
