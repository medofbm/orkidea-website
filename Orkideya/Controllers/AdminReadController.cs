using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Delivery;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminReadController(ApplicationDbContext context, TimeProvider clock) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var metrics = new DashboardMetrics(context, clock.GetUtcNow().UtcDateTime);
        var recent = await context.Orders.AsNoTracking().Include(order => order.Creation)
            .OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId)
            .Take(5).ToListAsync(cancellationToken);
        var thirtyDayValue = await metrics.ThirtyDaysAsync();
        var thirtyDayOrders = await metrics.ThirtyDayOrdersAsync();
        var today = await metrics.TodayAsync();
        var weeklyChange = await metrics.WeeklyChangeAsync();
        var statusCounts = await metrics.StatusCountsAsync();
        var topProducts = await metrics.TopProductsAsync(5);
        // Lowest available stock first (on hand minus reserved), for active products only.
        var lowestStock = await context.StockAccounts.AsNoTracking()
            .Join(context.Products.Where(product => !product.IsArchived), account => account.ProductId, product => product.ProductId,
                (account, product) => new { product.ProductId, product.Name, account.Unit, Available = account.OnHand - account.Reserved })
            .OrderBy(row => row.Available).Take(5).ToListAsync(cancellationToken);
        var visibleReviews = context.Reviews.AsNoTracking().Where(review => review.IsVisible);
        var reviewCount = await visibleReviews.CountAsync(cancellationToken);
        return Ok(new
        {
            productsValueLast30Days = Money(thirtyDayValue),
            pendingOrders = await metrics.PendingAsync(),
            productsWithoutVariants = await context.Products.CountAsync(product => !product.ProductVariants.Any(), cancellationToken),
            accounts = await context.Users.CountAsync(cancellationToken),
            recentOrders = recent.Select(Summary),
            ordersLast30Days = thirtyDayOrders,
            averageOrderValue30Days = thirtyDayOrders == 0 ? null : Money(thirtyDayValue / thirtyDayOrders),
            ordersToday = today.Orders,
            valueToday = Money(today.Value),
            weeklyChangePercent = weeklyChange is null ? (decimal?)null : Math.Round(weeklyChange.Value, 1),
            statusCounts = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" }
                .ToDictionary(status => status, status => statusCounts.GetValueOrDefault(status)),
            topProducts = topProducts.Select(product => new
            {
                product.ProductId, name = product.Name ?? "منتج #" + product.ProductId, product.Quantity, value = Money(product.Value)
            }),
            lowestStock = lowestStock.Select(row => new
            {
                row.ProductId, row.Name, row.Unit, available = row.Available.ToString("0.###", CultureInfo.InvariantCulture)
            }),
            unreadMessages = await context.ContactMessages.CountAsync(message => !message.IsRead, cancellationToken),
            reviewsCount = reviewCount,
            reviewsAverage = reviewCount == 0 ? (decimal?)null : Math.Round((decimal)await visibleReviews.AverageAsync(review => review.Rating, cancellationToken), 1)
        });
    }

    [HttpGet("sales-chart")]
    public async Task<IActionResult> SalesChart(int days = 7)
    {
        var chart = await new DashboardMetrics(context, clock.GetUtcNow().UtcDateTime).ChartAsync(days == 30 ? 30 : 7);
        return Ok(new { labels = chart.Labels, data = chart.Data });
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken cancellationToken,
        string status = "all", string search = "", int page = 1, string source = "all")
    {
        if (page < 1 || !OrderListFilter.TryCreate(status, search, out var filter, source))
            return BadRequest(new { message = "بيانات البحث أو رقم الصفحة غير صالح." });
        var result = await new OrderListQuery(context).PageAsync(filter, page, cancellationToken);
        return Ok(new { items = result.Orders.Select(Summary), result.Page, result.Pages, result.Total, result.PageSize });
    }

    [HttpGet("orders/export")]
    public async Task<IActionResult> ExportOrders([FromServices] ExcelExportService excel,
        CancellationToken cancellationToken, string status = "all", string search = "", string source = "all")
    {
        if (!OrderListFilter.TryCreate(status, search, out var filter, source))
            return BadRequest(new { message = "بيانات البحث غير صالحة." });

        var orders = await new OrderListQuery(context).Ordered(filter).ToListAsync(cancellationToken);
        return File(excel.ExportOrders(orders),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Orkideya_Orders_{clock.GetUtcNow():yyyyMMdd_HHmmss}.xlsx");
    }

    [HttpGet("delivery")]
    public async Task<IActionResult> Delivery(CancellationToken cancellationToken,
        string state = "active", string search = "", int page = 1)
    {
        var result = await new DeliveryListQuery(context).PageAsync(state, search, page, cancellationToken);
        if (result is null) return BadRequest(new { message = "حالة التوصيل أو البحث أو رقم الصفحة غير صالح." });

        return Ok(new
        {
            items = result.Rows.Select(row => new
            {
                row.OrderId, row.CustomerName, row.City, row.Mode, row.HandoffMode, row.CarrierName, row.ShipmentReference,
                row.State, UpdatedAtUtc = DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc)
            }),
            result.Page, result.Pages, result.Total
        });
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Order(int id, CancellationToken cancellationToken)
    {
        var snapshot = await (
            from candidate in context.Orders.AsNoTracking().Include(item => item.Creation)
            where candidate.OrderId == id
            join shipment in context.OrderDeliveries.AsNoTracking() on candidate.OrderId equals shipment.OrderId into shipments
            from shipment in shipments.DefaultIfEmpty()
            select new
            {
                candidate, shipment,
                carrierChoice = candidate.ShippingQuote == null ? null : new
                {
                    candidate.ShippingQuote.Area, candidate.ShippingQuote.ServiceKind, candidate.ShippingQuote.IsPickup
                }
            }).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null) return NotFound();
        var order = snapshot.candidate;
        var delivery = snapshot.shipment is { } currentShipment
            ? new { currentShipment.State, currentShipment.Mode, currentShipment.HandoffMode, currentShipment.CarrierName, currentShipment.ShipmentReference }
            : null;

        var items = await (
            from item in context.OrderItems.AsNoTracking()
            where item.OrderId == id
            join product in context.Products.AsNoTracking() on item.ProductId equals product.ProductId into products
            from product in products.DefaultIfEmpty()
            orderby item.OrderItemId
            select new
            {
                item.OrderItemId, item.ProductId, item.Quantity, item.VariantSize, item.UnitPrice,
                productName = product == null ? null : product.Name
            }).ToListAsync(cancellationToken);

        var deliveryHistory = await (
            from entry in context.OrderDeliveryEvents.AsNoTracking()
            where entry.OrderId == id && entry.Revision <= order.StatusRevision
            join actor in context.Users.AsNoTracking() on entry.ActorUserId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby entry.Revision descending
            select new
            {
                entry.Revision, entry.Action, entry.NewState, entry.Mode, entry.HandoffMode, entry.CarrierName,
                entry.ShipmentReference, entry.Reason, entry.ChangedAtUtc,
                actorName = actor == null ? null : actor.UserName
            }).Take(50).ToListAsync(cancellationToken);
        var history = await (
            from change in context.OrderStatusChanges.AsNoTracking()
            where change.OrderId == id && change.Revision <= order.StatusRevision
            join actor in context.Users.AsNoTracking() on change.ActorUserId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby change.Revision descending
            select new
            {
                change.Revision, change.PreviousStatus, change.NewStatus, change.Reason, change.ChangedAtUtc,
                actorName = actor == null ? null : actor.UserName
            }).Take(50).ToListAsync(cancellationToken);

        return Ok(new
        {
            order = Summary(order), order.WhatsAppNumber, order.ShippingAddress, order.PaymentMethod, snapshot.carrierChoice,
            order.StatusRevision, order.StockState, hasDelivery = delivery is not null, delivery,
            deliveryHistory = deliveryHistory.Select(entry => new
            {
                entry.Revision, entry.Action, entry.NewState, entry.Mode, entry.HandoffMode, entry.CarrierName,
                entry.ShipmentReference, entry.Reason, entry.actorName,
                ChangedAtUtc = DateTime.SpecifyKind(entry.ChangedAtUtc, DateTimeKind.Utc)
            }),
            history = history.Select(change => new
            {
                change.Revision, change.PreviousStatus, change.NewStatus, change.Reason, change.actorName,
                ChangedAtUtc = DateTime.SpecifyKind(change.ChangedAtUtc, DateTimeKind.Utc)
            }),
            productsTotal = Money(order.TotalAmount - order.ShippingCost),
            items = items.Select(item => new
            {
                id = item.OrderItemId, item.ProductId, item.Quantity, item.VariantSize, item.productName,
                unitPrice = Money(item.UnitPrice)
            })
        });
    }

    private static object Summary(Order order) => new
    {
        id = order.OrderId, order.CustomerName, order.City, order.OrderDate, order.Status,
        source = OrderSources.Label(order.Creation?.Source),
        total = Money(order.TotalAmount), shippingFee = Money(order.ShippingCost)
    };

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
