using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Shipping;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

/// <summary>
/// Admin page «العروض»: the single free-delivery promotion. Times are typed in Tripoli time
/// (Libya is UTC+2 all year) and stored in UTC. Nothing is created until the owner saves.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/promotions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPromotionsController(ApplicationDbContext db) : ControllerBase
{
    public static readonly TimeSpan TripoliOffset = TimeSpan.FromHours(2);
    private const string LocalFormat = "yyyy-MM-dd'T'HH:mm";

    public sealed record PromotionInput(bool IsEnabled, string? Title, string? Scope, string? StartsAtLocal,
        string? EndsAtLocal, string? MinSubtotal);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var promotion = await db.DeliveryPromotions.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
        return Ok(new { promotion = promotion is null ? null : View(promotion, DateTime.UtcNow) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] PromotionInput? input, CancellationToken ct)
    {
        var error = Validate(input, out var parsed);
        if (error is not null) return BadRequest(new { error });

        var promotion = await db.DeliveryPromotions.OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
        if (promotion is null)
        {
            promotion = new DeliveryPromotion { Title = parsed.Title };
            db.DeliveryPromotions.Add(promotion);
        }
        promotion.Title = parsed.Title;
        promotion.IsEnabled = parsed.IsEnabled;
        promotion.Scope = parsed.Scope;
        promotion.StartsAtUtc = parsed.StartsAtUtc;
        promotion.EndsAtUtc = parsed.EndsAtUtc;
        promotion.MinSubtotal = parsed.MinSubtotal;
        promotion.UpdatedAtUtc = DateTime.UtcNow;
        var user = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        promotion.UpdatedBy = user is { Length: > 450 } ? user[..450] : user;
        await db.SaveChangesAsync(ct);
        return Ok(new { promotion = View(promotion, DateTime.UtcNow) });
    }

    public sealed record ParsedPromotion(bool IsEnabled, string Title, string Scope, DateTime StartsAtUtc,
        DateTime EndsAtUtc, decimal? MinSubtotal);

    /// <summary>Validates admin input. Returns an Arabic error or null.</summary>
    public static string? Validate(PromotionInput? input, out ParsedPromotion parsed)
    {
        parsed = null!;
        if (input is null) return "البيانات ناقصة.";
        var title = input.Title?.Trim() ?? "";
        if (title.Length is 0 or > 80 || title.Any(char.IsControl)) return "اكتبي اسم العرض (لين 80 حرف).";
        if (input.Scope is not (DeliveryPromotion.ScopeTripoli or DeliveryPromotion.ScopeAllLibya)) return "اختاري مكان العرض.";
        if (!TryLocal(input.StartsAtLocal, out var starts) || !TryLocal(input.EndsAtLocal, out var ends))
            return "اكتبي وقت البداية والنهاية.";
        if (ends <= starts) return "وقت النهاية لازم يكون بعد البداية.";
        decimal? min = null;
        if (!string.IsNullOrWhiteSpace(input.MinSubtotal))
        {
            if (!decimal.TryParse(input.MinSubtotal.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ||
                value < 0 || value > 1000000 || decimal.Round(value, 2) != value)
                return "أقل قيمة للطلب لازم تكون رقم صحيح (مثلاً 100).";
            min = value;
        }
        parsed = new(input.IsEnabled, title, input.Scope, starts, ends, min);
        return null;
    }

    private static bool TryLocal(string? value, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(value?.Trim(), LocalFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return false;
        utc = DateTime.SpecifyKind(local - TripoliOffset, DateTimeKind.Utc);
        return true;
    }

    public static string Local(DateTime utc) => (utc + TripoliOffset).ToString(LocalFormat, CultureInfo.InvariantCulture);

    /// <summary>شغال توا / مجدول / منتهي / موقف.</summary>
    public static string Status(DeliveryPromotion promotion, DateTime nowUtc) =>
        !promotion.IsEnabled ? "stopped" :
        nowUtc < promotion.StartsAtUtc ? "scheduled" :
        nowUtc >= promotion.EndsAtUtc ? "ended" : "active";

    private static object View(DeliveryPromotion p, DateTime nowUtc) => new
    {
        p.IsEnabled, p.Title, p.Scope,
        startsAtLocal = Local(p.StartsAtUtc), endsAtLocal = Local(p.EndsAtUtc),
        startsAtUtc = DateTime.SpecifyKind(p.StartsAtUtc, DateTimeKind.Utc),
        endsAtUtc = DateTime.SpecifyKind(p.EndsAtUtc, DateTimeKind.Utc),
        minSubtotal = p.MinSubtotal?.ToString("0.00", CultureInfo.InvariantCulture),
        updatedAtUtc = DateTime.SpecifyKind(p.UpdatedAtUtc, DateTimeKind.Utc),
        status = Status(p, nowUtc),
        active = DeliveryPromotionRule.IsActive(p, nowUtc)
    };
}
