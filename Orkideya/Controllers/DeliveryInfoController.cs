using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Infrastructure.Shipping;
using System.Globalization;

namespace Orkideya.Controllers;

/// <summary>Read-only delivery information shown to customers before checkout. The server owns every price.</summary>
[ApiController]
[Route("api/v1/delivery")]
public sealed class DeliveryInfoController(ApplicationDbContext context, SabilCheckoutQuotes? quotes = null) : ControllerBase
{
    [HttpGet("summary")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        // Carrier quotes are per destination and only known at checkout, so no prices are exposed.
        if (quotes?.CheckoutEnabled == true) return Ok(new { mode = "carrier" });

        var rows = await context.ShippingRates.AsNoTracking()
            .Where(rate => rate.Price >= 0 && rate.CityName != "")
            .Select(rate => new { city = rate.CityName, region = rate.Region, price = rate.Price })
            .ToListAsync(cancellationToken);
        var rates = rows.OrderBy(r => r.price).ThenBy(r => r.city, StringComparer.Ordinal)
            .Select(r => new { r.city, price = Money(r.price) }).ToList();
        var groups = DeliveryRateGroups.Build(rows.Select(r => (r.region, r.city, r.price)))
            .Select(g => new
            {
                city = g.City, minPrice = Money(g.MinPrice), maxPrice = Money(g.MaxPrice),
                areas = g.Areas.Select(a => new { area = a.Area, price = Money(a.Price) }).ToList()
            }).ToList();
        return Ok(new
        {
            mode = "rates",
            minPrice = Money(rows.Count == 0 ? 0m : rows.Min(r => r.price)),
            maxPrice = Money(rows.Count == 0 ? 0m : rows.Max(r => r.price)),
            cities = groups.Count,
            areas = rows.Count,
            rates,
            groups
        });
    }

    /// <summary>The free-delivery promotion when it is active right now (for the storefront banner), else null.</summary>
    [HttpGet("promotion")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Promotion(CancellationToken cancellationToken)
    {
        var promotion = await new Orkideya.Application.Shipping.DeliveryPromotionService(context)
            .GetActiveAsync(DateTime.UtcNow, cancellationToken);
        return Ok(new
        {
            promotion = promotion is null ? null : new
            {
                title = promotion.Title, scope = promotion.Scope,
                endsAtUtc = DateTime.SpecifyKind(promotion.EndsAtUtc, DateTimeKind.Utc),
                minSubtotal = promotion.MinSubtotal is { } min ? Money(min) : null
            }
        });
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
