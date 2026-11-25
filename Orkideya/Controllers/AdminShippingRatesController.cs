using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orkideya.Application.Shipping;
using Orkideya.Data;
using Orkideya.Infrastructure.Shipping;

namespace Orkideya.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/shipping-rates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminShippingRatesController(ApplicationDbContext db, SabilRateImport import,
    IOptions<SabilReadOptions> options, ILogger<AdminShippingRatesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rows = await db.ShippingRates.AsNoTracking()
            .OrderBy(r => r.Region).ThenBy(r => r.Price).ThenBy(r => r.CityName)
            .Select(r => new { r.ShippingRateId, r.Region, r.CityName, r.Price }).ToListAsync(ct);
        return Ok(new
        {
            readEnabled = options.Value.ReadEnabled,
            total = rows.Count,
            rates = rows.Select(r => new
            {
                id = r.ShippingRateId, region = r.Region, city = r.CityName, price = Money(r.Price)
            })
        });
    }

    [HttpPost("import-sabil"), ValidateAntiForgeryToken, Orkideya.Infrastructure.Http.Idempotent]
    public async Task<IActionResult> ImportSabil(CancellationToken ct, bool dryRun = true, string? surcharge = null)
    {
        if (!SabilSurcharge.TryParse(surcharge, out var extra))
            return BadRequest(new { error = "قيمة الإضافة غير صحيحة" });
        var result = await import.PlanAsync(extra, ct);
        if (result.State == CarrierReadState.Disabled)
            return Conflict(new { error = "ربط درب السبيل غير مفعّل على الخادم" });
        if (!result.Succeeded)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "تعذر قراءة أسعار درب السبيل الآن", state = result.State.ToString() });

        var plan = result.Plan!;
        var applied = false;
        if (!dryRun)
        {
            applied = await import.ApplyAsync(plan, ct);
            if (!applied) return UnprocessableEntity(new { error = "لا توجد أسعار صالحة للاستيراد" });
            logger.LogInformation("Sabil delivery rates imported: {Areas} areas in {Cities} cities", plan.Areas, plan.Cities);
        }

        return Ok(new
        {
            applied,
            cities = plan.Cities, areas = plan.Areas, conflicts = plan.Conflicts, skippedNoRate = plan.SkippedNoRate,
            skippedPickup = plan.SkippedPickup, surcharge = Money(plan.Surcharge),
            sample = plan.Rows.Take(10).Select(r => new { region = r.City, area = r.Area, price = Money(r.Price) })
        });
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
