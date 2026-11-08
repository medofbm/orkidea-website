using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/categories")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCategoriesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, string? search = null, int page = 1)
    {
        search = search?.Trim() ?? "";
        if (search.Length > 100 || page < 1) return BadRequest();

        const int pageSize = 25;
        var query = db.Categories.AsNoTracking();
        if (search.Length != 0) query = query.Where(category => category.Name.Contains(search));
        var total = await query.CountAsync(ct);
        var pages = (int)Math.Max(1L, (total + (long)pageSize - 1) / pageSize);
        page = Math.Min(page, pages);
        var items = await query.OrderBy(category => category.Name).ThenBy(category => category.CategoryId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(category => new
            {
                category.CategoryId, category.Name,
                products = db.Products.Count(product => product.CategoryId == category.CategoryId)
            }).ToListAsync(ct);
        return Ok(new { items, page, pages, total });
    }

    public sealed record CreateInput([Required, StringLength(150)] string Name);
    public sealed record EditInput([Required, StringLength(150)] string Name, [Required] string ExpectedName);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CreateInput input, CancellationToken ct) =>
        Save(null, input.Name.Trim(), null, ct);

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Rename(int id, EditInput input, CancellationToken ct) =>
        Save(id, input.Name.Trim(), input.ExpectedName, ct);

    private async Task<IActionResult> Save(int? id, string name, string? expectedName, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(() => SaveOnce(id, name, expectedName, ct));
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return Stale();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            return Stale();
        }
    }

    private async Task<IActionResult> SaveOnce(int? id, string name, string? expectedName, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        // Serialize the lookup and write so concurrent Vue requests cannot create the same name.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (id is int categoryId)
        {
            var category = await db.Categories.SingleOrDefaultAsync(row => row.CategoryId == categoryId, ct);
            if (category is null) return NotFound(new { message = "الفئة غير موجودة." });
            if (!string.Equals(category.Name, expectedName, StringComparison.Ordinal)) return Stale();
            if (category.Name == name) return Ok(new { success = true });
            if (await NameTaken(name, categoryId, ct)) return Duplicate();
            category.Name = name;
        }
        else
        {
            if (await NameTaken(name, null, ct)) return Duplicate();
            db.Categories.Add(new Category { Name = name });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true });
    }

    private Task<bool> NameTaken(string name, int? excludeId, CancellationToken ct) =>
        db.Categories.AnyAsync(category => category.CategoryId != excludeId && category.Name.Trim() == name, ct);

    private IActionResult Duplicate() => BadRequest(new { message = "فيه فئة بالاسم هذا بالفعل." });
    private IActionResult Stale() => Conflict(new { message = "تغيّرت الفئات. حدّث القائمة قبل إعادة المحاولة." });
}
