using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Inventory;
using Orkideya.Areas.Admin.Models;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/inventory")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminInventoryController(ApplicationDbContext db, StockService stock,
    ILogger<AdminInventoryController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, string? search = null, int page = 1)
    {
        search = search?.Trim() ?? "";
        if (search.Length > 100 || page < 1) return BadRequest(new { message = "البحث أو رقم الصفحة غير صالح." });

        var products = db.Products.AsNoTracking();
        if (search.Length > 0) products = products.Where(product => product.Name.Contains(search));
        var total = await products.CountAsync(ct);
        const int pageSize = 25;
        var pages = PageCount(total, pageSize);
        page = Math.Min(page, pages);

        var rows = await (
            from product in products.OrderBy(product => product.ProductId).Skip((page - 1) * pageSize).Take(pageSize)
            join account in db.StockAccounts.AsNoTracking() on product.ProductId equals account.ProductId into accounts
            from account in accounts.DefaultIfEmpty()
            orderby product.ProductId
            select new { product.ProductId, product.Name, Account = account }).ToListAsync(ct);

        return Ok(new { items = rows.Select(row => new { row.ProductId, row.Name, account = Summary(row.Account) }), total, page, pages });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct, int page = 1)
    {
        if (page < 1) return BadRequest();
        var item = await (
            from product in db.Products.AsNoTracking()
            where product.ProductId == id
            join account in db.StockAccounts.AsNoTracking() on product.ProductId equals account.ProductId into accounts
            from account in accounts.DefaultIfEmpty()
            select new
            {
                product.ProductId, product.Name, Account = account,
                Variants = product.ProductVariants.OrderBy(variant => variant.ProductVariantId)
                    .Select(variant => new { variant.ProductVariantId, variant.Size, variant.StockUnitsPerItem }).ToList()
            }).SingleOrDefaultAsync(ct);
        if (item is null) return NotFound();

        const int pageSize = 50;
        var query = db.StockMovements.AsNoTracking().Where(move => move.StockAccountId == id);
        var total = await query.CountAsync(ct);
        var pages = PageCount(total, pageSize);
        page = Math.Min(page, pages);
        var moves = await (
            from move in query
            join actor in db.Users.AsNoTracking() on move.ActorUserId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby move.CreatedAtUtc descending, move.StockMovementId descending
            select new
            {
                move.StockMovementId, move.OrderId, move.Kind, move.OnHandDelta, move.ReservedDelta, move.Reason, move.CreatedAtUtc,
                actorName = actor == null ? null : actor.UserName
            }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new
        {
            item.ProductId, item.Name, account = Summary(item.Account), item.Variants, total, page, pages,
            movements = moves.Select(move => new
            {
                move.StockMovementId, move.OrderId, move.Kind, move.OnHandDelta, move.ReservedDelta, move.Reason, move.actorName,
                CreatedAtUtc = DateTime.SpecifyKind(move.CreatedAtUtc, DateTimeKind.Utc)
            })
        });
    }

    public sealed record OpenInput([Required, StringLength(5)] string Unit);

    [HttpPost("{id:int}/open"), ValidateAntiForgeryToken]
    public Task<IActionResult> Open(int id, OpenInput input, CancellationToken ct) =>
        Write(id, actor => stock.OpenAsync(id, input.Unit, actor, ct));

    [HttpPost("{id:int}/configure"), ValidateAntiForgeryToken]
    public Task<IActionResult> Configure(int id, StockVariantInput input, CancellationToken ct) =>
        Write(id, actor => stock.ConfigureAsync(id, input.VariantId, input.Units!.Value, input.ExpectedRevision!.Value, actor, ct));

    [HttpPost("{id:int}/adjust"), ValidateAntiForgeryToken]
    public Task<IActionResult> Adjust(int id, StockAdjustmentInput input, CancellationToken ct) =>
        Write(id, actor => stock.AdjustAsync(id, input.Delta!.Value, input.ExpectedRevision!.Value, input.RequestId!.Value, actor, input.Reason, ct));

    private async Task<IActionResult> Write(int id, Func<string, Task<StockResult>> operation)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null || !StockService.ValidActor(actor)) return Forbid();
        try
        {
            return await operation(actor) switch
            {
                StockResult.Changed => Ok(new { success = true, message = "تم حفظ حركة المخزون." }),
                StockResult.Unchanged => Ok(new { success = true, message = "البيانات محفوظة بالفعل." }),
                StockResult.Conflict => Conflict(new { message = "تغيّر الرصيد أو الإعداد. حدّث المخزون قبل إعادة المحاولة." }),
                StockResult.Missing => NotFound(new { message = "المنتج أو الحجم غير موجود." }),
                StockResult.Insufficient => BadRequest(new { message = "الرصيد بعد التعديل خارج الحدود أو أقل من الكمية المحجوزة." }),
                _ => BadRequest(new { message = "راجع وحدة القياس والكمية وسبب التعديل." })
            };
        }
        catch (DbUpdateException)
        {
            logger.LogError("Inventory update failed for product {ProductId}", id);
            return StatusCode(500, new { message = "تعذر التحقق من حفظ المخزون. حدّث الرصيد قبل إعادة المحاولة." });
        }
    }

    private static int PageCount(int total, int size) => (int)Math.Max(1L, (total + (long)size - 1) / size);

    private static object? Summary(StockAccount? account) => account is null ? null : new
    {
        account.Unit, account.OnHand, account.Reserved, account.Revision,
        available = account.OnHand - account.Reserved
    };
}
