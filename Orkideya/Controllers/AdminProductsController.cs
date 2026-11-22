using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Products;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/products")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminProductsController(ApplicationDbContext db, ProductEditor editor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, string? search = null, int page = 1)
    {
        search = search?.Trim() ?? "";
        if (search.Length > 100 || page < 1) return BadRequest();

        const int pageSize = 25;
        var query = db.Products.AsNoTracking();
        if (search.Length != 0) query = query.Where(product => product.Name.Contains(search));
        var total = await query.CountAsync(ct);
        var pages = (int)Math.Max(1L, (total + (long)pageSize - 1) / pageSize);
        page = Math.Min(page, pages);
        var items = await query.OrderBy(product => product.ProductId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(product => new
            {
                product.ProductId, product.Name, product.ImageUrl, product.IsArchived,
                category = product.Category == null ? null : product.Category.Name,
                variants = product.ProductVariants.Count,
                pricedVariants = product.ProductVariants.Count(variant => variant.Price > 0),
                hasDescription = product.Description != null && product.Description.Trim() != ""
            }).ToListAsync(ct);
        return Ok(new { items, page, pages, total });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await db.Categories.AsNoTracking()
        .OrderBy(category => category.Name).ThenBy(category => category.CategoryId)
        .Select(category => new { category.CategoryId, category.Name, category.IsArchived }).ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Include(product => product.ProductVariants)
            .SingleOrDefaultAsync(product => product.ProductId == id, ct);
        if (product is null) return NotFound();
        return Ok(new
        {
            product.ProductId, product.Name, product.Description, product.CategoryId, product.ImageUrl, product.IsArchived,
            gallery = product.GalleryImages, version = Version(product),
            variants = product.ProductVariants.OrderBy(variant => variant.ProductVariantId).Select(variant => new
            {
                variant.ProductVariantId, variant.Size, price = Money(variant.Price), variant.IsArchived
            })
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] ProductInput input, IFormFile? imageFile,
        [FromForm] List<IFormFile>? galleryFiles, CancellationToken ct)
    {
        await editor.ValidateInputAsync(input, ModelState, ct);
        var gallery = await editor.SaveGalleryAsync(input, [], galleryFiles, imageFile, ModelState, ct);
        if (gallery is null) return ValidationProblem(ModelState);
        var product = new Product
        {
            Name = input.Name.Trim(), Description = input.Description, CategoryId = input.CategoryId,
            ImageUrl = input.ImageUrl, GalleryJson = ProductEditor.SerializeGallery(gallery)
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, product.ProductId });
    }

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, [FromForm] ProductInput input, IFormFile? imageFile,
        [FromForm] List<IFormFile>? galleryFiles, [FromForm] List<string>? removeGallery,
        [FromForm] string version, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.ProductId == id, ct);
        if (product is null) return NotFound();
        if (version != Version(product)) return Stale();

        await editor.ValidateInputAsync(input, ModelState, ct);
        var retained = product.GalleryImages.Where(url => removeGallery?.Contains(url) != true).ToList();
        var gallery = await editor.SaveGalleryAsync(input, retained, galleryFiles, imageFile, ModelState, ct);
        if (gallery is null) return ValidationProblem(ModelState);

        var name = input.Name.Trim();
        var galleryJson = ProductEditor.SerializeGallery(gallery);
        var changed = await db.Products.Where(row => row.ProductId == id && row.CategoryId == product.CategoryId &&
                row.Name == product.Name && row.Description == product.Description &&
                row.ImageUrl == product.ImageUrl && row.GalleryJson == product.GalleryJson)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Name, name)
                .SetProperty(row => row.Description, input.Description).SetProperty(row => row.CategoryId, input.CategoryId)
                .SetProperty(row => row.ImageUrl, input.ImageUrl).SetProperty(row => row.GalleryJson, galleryJson), ct);
        return changed == 1 ? Ok(new { success = true, productId = id }) : Stale();
    }

    [HttpPost("{id:int}/variants"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(int id, ProductVariantInput input, CancellationToken ct)
    {
        if (id != input.ProductId) return BadRequest();
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return NotFound();
        db.ProductVariants.Add(new ProductVariant { Product = product, ProductId = id, Size = input.Size.Trim(), Price = input.Price });
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    public sealed record VariantEditInput(
        [Required] ProductVariantInput Input,
        [Required] string ExpectedSize,
        [Required] decimal? ExpectedPrice);

    [HttpPost("{id:int}/variants/{variantId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVariant(int id, int variantId, VariantEditInput request, CancellationToken ct)
    {
        var input = request.Input;
        if (id != input.ProductId || variantId != input.ProductVariantId) return BadRequest();
        var size = input.Size.Trim();
        var changed = await db.ProductVariants.Where(variant => variant.ProductVariantId == variantId && variant.ProductId == id &&
                variant.Size == request.ExpectedSize && variant.Price == request.ExpectedPrice)
            .ExecuteUpdateAsync(setters => setters.SetProperty(variant => variant.Size, size)
                .SetProperty(variant => variant.Price, input.Price), ct);
        return changed == 1 ? Ok(new { success = true }) : Stale();
    }

    public sealed record ArchiveInput(bool Archived);

    [HttpPost("{id:int}/archive"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, [FromBody] ArchiveInput? input, CancellationToken ct)
    {
        if (input is null) return BadRequest(new { message = "البيانات ناقصة." });
        var product = await db.Products.SingleOrDefaultAsync(row => row.ProductId == id, ct);
        if (product is null) return NotFound();
        product.IsArchived = input.Archived;
        await db.SaveChangesAsync(ct);
        return Ok(new { id = product.ProductId, isArchived = product.IsArchived });
    }

    [HttpPost("{productId:int}/variants/{variantId:int}/archive"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveVariant(int productId, int variantId, [FromBody] ArchiveInput? input, CancellationToken ct)
    {
        if (input is null) return BadRequest(new { message = "البيانات ناقصة." });
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var product = await db.Products.SingleOrDefaultAsync(row => row.ProductId == productId, ct);
            var variants = await db.ProductVariants.Where(row => row.ProductId == productId).ToListAsync(ct);
            var variant = variants.SingleOrDefault(row => row.ProductVariantId == variantId);
            if (product is null || variant is null) return NotFound();
            // A visible product must always keep at least one visible size.
            if (input.Archived && !product.IsArchived && !variant.IsArchived && variants.Count(row => !row.IsArchived) == 1)
                return Conflict(new { message = "هذا آخر حجم ظاهر للمنتج. أرشفي المنتج كامل بدل الحجم." });
            variant.IsArchived = input.Archived;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok(new { id = variant.ProductVariantId, productId, isArchived = variant.IsArchived });
        });
    }

    // Products that appear in orders or have stock history are kept (OrderItem.ProductId has no FK).
    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var product = await db.Products.Include(row => row.ProductVariants)
                .SingleOrDefaultAsync(row => row.ProductId == id, ct);
            if (product is null) return NotFound();
            if (await db.OrderItems.AnyAsync(item => item.ProductId == id, ct) ||
                await db.StockAccounts.AnyAsync(account => account.ProductId == id, ct))
                return Conflict(new { message = "ما ينفعش نحذف منتج عنده طلبات أو مخزون. تقدري تأرشفيه عشان يختفي من المتجر." });
            // Same as the Razor delete: rows only; image files are not removed (they may be shared).
            db.ProductVariants.RemoveRange(product.ProductVariants);
            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return NoContent();
        });
    }

    // Order lines keep a VariantSize snapshot and carts live in the session, so deleting a variant is safe.
    [HttpPost("{id:int}/variants/{variantId:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariant(int id, int variantId, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var variants = await db.ProductVariants.Where(row => row.ProductId == id).ToListAsync(ct);
            var variant = variants.SingleOrDefault(row => row.ProductVariantId == variantId);
            if (variant is null) return NotFound();
            if (variants.Count == 1)
                return Conflict(new { message = "هذا آخر حجم للمنتج. أضيفي حجم ثاني أول أو احذفي المنتج كامل." });
            db.ProductVariants.Remove(variant);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return NoContent();
        });
    }

    private IActionResult Stale() => Conflict(new { message = "تغيّرت البيانات. حدّث صفحة المنتج قبل إعادة الحفظ." });

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Version(Product product) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new { product.ProductId, product.Name, product.Description, product.CategoryId, product.ImageUrl, product.GalleryJson })));
}
