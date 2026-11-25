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
    public const int MaxSearchLength = 100;
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.Ordinal) { "default", "newest", "price-asc", "price-desc", "name" };

    [HttpGet("products")]
    public async Task<IActionResult> Products(int page = 1, CancellationToken cancellationToken = default, int? categoryId = null,
        string? search = null, string? sort = null)
    {
        if (page < 1 || page > 10000) return BadRequest(new ProblemDetails { Title = "رقم الصفحة غير صالح." });
        // Validated before any database access.
        var term = search?.Trim();
        if (term is { Length: > MaxSearchLength }) return BadRequest(new ProblemDetails { Title = "نص البحث طويل جدًا." });
        var sortKey = string.IsNullOrWhiteSpace(sort) ? "default" : sort.Trim();
        if (!AllowedSorts.Contains(sortKey)) return BadRequest(new ProblemDetails { Title = "طريقة الترتيب غير صالحة." });
        const int pageSize = 24;
        var query = context.Products.AsNoTracking().Where(product => !product.IsArchived &&
            product.ProductVariants.Any(variant => !variant.IsArchived && variant.Price > 0));
        var categories = await query.Where(product => product.Category != null && !product.Category.IsArchived)
            .Select(product => new { id = product.Category!.CategoryId, name = product.Category.Name })
            .Distinct().OrderBy(category => category.name).ThenBy(category => category.id).ToListAsync(cancellationToken);
        if (categoryId.HasValue)
            query = query.Where(product => product.CategoryId == categoryId);
        if (!string.IsNullOrEmpty(term))
            query = query.Where(product => product.Name.Contains(term) || (product.Category != null && product.Category.Name.Contains(term)));
        var total = await query.CountAsync(cancellationToken);
        // ProductId is always the final tie-breaker so paging is stable.
        var ordered = sortKey switch
        {
            "newest" => query.OrderByDescending(product => product.ProductId),
            "price-asc" => query.OrderBy(product => product.ProductVariants.Where(variant => !variant.IsArchived && variant.Price > 0).Min(variant => variant.Price)).ThenBy(product => product.ProductId),
            "price-desc" => query.OrderByDescending(product => product.ProductVariants.Where(variant => !variant.IsArchived && variant.Price > 0).Min(variant => variant.Price)).ThenBy(product => product.ProductId),
            "name" => query.OrderBy(product => product.Name).ThenBy(product => product.ProductId),
            _ => query.OrderBy(product => product.ProductId)
        };
        var products = await ordered.Include(product => product.ProductVariants.Where(variant => !variant.IsArchived)).Include(product => product.Category)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { items = products.Select(ToResponse), page, pageSize, total, categories });
    }

    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id, CancellationToken cancellationToken)
    {
        var product = await context.Products.AsNoTracking().Include(product => product.ProductVariants.Where(variant => !variant.IsArchived))
            .Include(product => product.Category).SingleOrDefaultAsync(product => product.ProductId == id && !product.IsArchived, cancellationToken);
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
        variants = product.ProductVariants.Where(variant => !variant.IsArchived && variant.Price > 0).OrderBy(variant => variant.Price).ThenBy(variant => variant.ProductVariantId)
            .Select(variant => new { id = variant.ProductVariantId, size = variant.Size,
                price = variant.Price.ToString("0.00", CultureInfo.InvariantCulture), currency = "LYD" }).ToArray()
    };
}
