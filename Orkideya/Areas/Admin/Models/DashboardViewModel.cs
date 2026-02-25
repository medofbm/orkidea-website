using Orkideya.Models;

namespace Orkideya.Areas.Admin.Models
{
    /// <summary>
    /// Dashboard View Model - Main analytics data
    /// </summary>
    public class DashboardViewModel
    {
        public decimal TotalSales { get; set; }
        public int PendingOrdersCount { get; set; }
        public int LowStockProductsCount { get; set; }
        public int TotalUsersCount { get; set; }
        public decimal SalesChangePercentage { get; set; }
        public required SalesChartDataViewModel SalesChartData { get; set; }
        public required CategoriesDataViewModel CategoriesData { get; set; }
        public required List<Order> RecentOrders { get; set; }
    }

    /// <summary>
    /// Sales Chart Data for Chart.js Line Chart
    /// </summary>
    public class SalesChartDataViewModel
    {
        public required List<string> Labels { get; set; }
        public required List<decimal> Data { get; set; }
    }

    /// <summary>
    /// Categories Distribution Data for Chart.js Doughnut Chart
    /// </summary>
    public class CategoriesDataViewModel
    {
        public required List<string> Labels { get; set; }
        public required List<int> Data { get; set; }
    }
}
