using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

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
        return Ok(new
        {
            productsValueLast30Days = Money(await metrics.ThirtyDaysAsync()),
            pendingOrders = await metrics.PendingAsync(),
            productsWithoutVariants = await context.Products.CountAsync(product => !product.ProductVariants.Any(), cancellationToken),
            accounts = await context.Users.CountAsync(cancellationToken),
            recentOrders = recent.Select(Summary)
        });
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

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Order(int id, CancellationToken cancellationToken)
    {
        var order = await context.Orders.AsNoTracking().Include(item => item.Creation)
            .SingleOrDefaultAsync(item => item.OrderId == id, cancellationToken);
        if (order is null) return NotFound();

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

        var hasDelivery = await context.OrderDeliveries.AsNoTracking()
            .AnyAsync(delivery => delivery.OrderId == id, cancellationToken);
        var history = await (
            from change in context.OrderStatusChanges.AsNoTracking()
            where change.OrderId == id
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
            order = Summary(order), order.WhatsAppNumber, order.ShippingAddress, order.PaymentMethod,
            order.StatusRevision, order.StockState, hasDelivery,
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
