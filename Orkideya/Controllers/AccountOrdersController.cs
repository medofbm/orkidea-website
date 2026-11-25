using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

/// <summary>
/// "My orders" for a signed-in customer. Only orders whose CustomerUserId equals the current
/// Identity user id are visible; anything else is a 404 so existence is never revealed.
/// Guest and legacy orders are intentionally not matched (no phone matching).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/account/orders")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountOrdersController(ApplicationDbContext context) : ControllerBase
{
    private const int MaxOrders = 50;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var rows = await context.Orders.AsNoTracking()
            .Where(order => order.CustomerUserId == userId)
            .OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId)
            .Take(MaxOrders)
            .Select(order => new
            {
                order.OrderId, order.OrderDate, order.Status, order.TotalAmount,
                ItemsCount = order.Items.Sum(item => item.Quantity)
            }).ToListAsync(cancel);
        return Ok(new
        {
            items = rows.Select(row => new
            {
                id = row.OrderId,
                orderDate = DateTime.SpecifyKind(row.OrderDate, DateTimeKind.Utc),
                status = row.Status,
                statusLabel = Label(row.Status),
                total = Money(row.TotalAmount),
                itemsCount = row.ItemsCount
            })
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || id <= 0) return NotFound();
        var order = await context.Orders.AsNoTracking()
            .Where(row => row.OrderId == id && row.CustomerUserId == userId)
            .Select(row => new
            {
                row.OrderId, row.OrderDate, row.Status, row.City, row.ShippingCost, row.TotalAmount,
                Lines = row.Items.OrderBy(item => item.OrderItemId).Select(item => new
                {
                    ProductName = context.Products.Where(product => product.ProductId == item.ProductId)
                        .Select(product => product.Name).FirstOrDefault(),
                    item.VariantSize, item.Quantity, item.UnitPrice
                }).ToList()
            }).SingleOrDefaultAsync(cancel);
        if (order is null) return NotFound();
        return Ok(new
        {
            id = order.OrderId,
            orderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc),
            status = order.Status,
            statusLabel = Label(order.Status),
            city = order.City,
            productsTotal = Money(order.Lines.Sum(line => line.Quantity * line.UnitPrice)),
            shippingFee = Money(order.ShippingCost),
            total = Money(order.TotalAmount),
            lines = order.Lines.Select(line => new
            {
                productName = line.ProductName ?? "",
                size = line.VariantSize,
                quantity = line.Quantity,
                unitPrice = Money(line.UnitPrice),
                subtotal = Money(line.Quantity * line.UnitPrice)
            })
        });
    }

    private static string Label(string status) => status switch
    {
        "Pending" => "جديد",
        "Processing" => "قيد التجهيز",
        "Shipped" => "قيد التوصيل",
        "Delivered" => "مسلّم",
        "Cancelled" => "ملغي",
        _ => "قيد المراجعة"
    };

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
