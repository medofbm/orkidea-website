using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

/// <summary>Public customer reviews. Only the first word of a name is returned, and only with the customer's consent; new reviews wait for admin approval.</summary>
[ApiController]
[Route("api/v1/reviews")]
public sealed class ReviewsApiController(ApplicationDbContext db) : ControllerBase
{
    public sealed record ReviewInput(string? Name, int Rating, string? Message);

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Latest(CancellationToken ct)
    {
        var rows = await db.Reviews.AsNoTracking().Where(review => review.IsVisible)
            .OrderByDescending(review => review.Id).Take(12)
            .Select(review => new { review.Id, review.Message, review.Rating, review.Source, review.CustomerName, review.AllowName })
            .ToListAsync(ct);
        // Only the first word of the name, and only when the customer agreed to show it.
        var items = rows.Select(review => new
        {
            id = review.Id, message = review.Message, rating = review.Rating, source = review.Source,
            name = review.AllowName ? FirstWord(review.CustomerName) : null
        });
        return Ok(new { items });
    }

    /// <summary>First word of a name, or null when there is none.</summary>
    public static string? FirstWord(string? fullName)
    {
        var word = (fullName ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrEmpty(word) ? null : word;
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Create([FromBody] ReviewInput? input, CancellationToken ct)
    {
        var error = Validate(input, out var name, out var message);
        if (error is not null) return BadRequest(new { message = error });
        // The model default is IsVisible = true, so the flag is set explicitly: reviews need approval first.
        db.Reviews.Add(new Review { CustomerName = name, Message = message, Rating = input!.Rating, IsVisible = false });
        await db.SaveChangesAsync(ct);
        return Accepted(new { message = "شكرًا! رأيك وصلنا وبيظهر بعد المراجعة." });
    }

    public static string? Validate(ReviewInput? input, out string name, out string message)
    {
        name = input?.Name?.Trim() ?? "";
        message = input?.Message?.Trim() ?? "";
        if (input is null) return "البيانات ناقصة، حاولي مرة ثانية.";
        if (name.Length < 2 || name.Length > 60) return "اكتبي اسمك (من حرفين إلى 60 حرفًا).";
        if (input.Rating < 1 || input.Rating > 5) return "اختاري تقييم من 1 إلى 5.";
        if (message.Length < 10 || message.Length > 500) return "اكتبي رأيك بين 10 و500 حرف.";
        return null;
    }
}
