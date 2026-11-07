using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using System.Globalization;

namespace Orkideya.Controllers;

[ApiController]
[Route("api/v1/catalogue")]
public sealed class CatalogueController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("products")]
    public async Task<IActionResult> Products(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 10000) return BadRequest(new ProblemDetails { Title = "رقم الصفحة غير صالح." });
        const int pageSize = 24;
        var query = context.Products.AsNoTracking().Where(product => product.ProductVariants.Any(variant => variant.Price > 0));
        var total = await query.CountAsync(cancellationToken);
        var products = await query.Include(product => product.ProductVariants).Include(product => product.Category)
            .OrderBy(product => product.ProductId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { items = products.Select(ToResponse), page, pageSize, total });
    }

    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id, CancellationToken cancellationToken)
    {
        var product = await context.Products.AsNoTracking().Include(product => product.ProductVariants)
            .Include(product => product.Category).SingleOrDefaultAsync(product => product.ProductId == id, cancellationToken);
        return product is null ? NotFound(new ProblemDetails { Title = "المنتج غير موجود." }) : Ok(ToResponse(product));
    }

    private static object ToResponse(Product product) => new
    {
        id = product.ProductId,
        name = product.Name,
        description = product.Description,
        category = product.Category?.Name,
        images = new[] { product.ImageUrl }.Concat(product.GalleryImages)
            .Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().ToArray(),
        variants = product.ProductVariants.Where(variant => variant.Price > 0).OrderBy(variant => variant.Price).ThenBy(variant => variant.ProductVariantId)
            .Select(variant => new { id = variant.ProductVariantId, size = variant.Size,
                price = variant.Price.ToString("0.00", CultureInfo.InvariantCulture), currency = "LYD" }).ToArray()
    };
}
