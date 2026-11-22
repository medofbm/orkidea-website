using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;

namespace Orkideya.Controllers;

/// <summary>Admin moderation of customer reviews and contact messages (GET and POST only).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminFeedbackController(ApplicationDbContext db) : ControllerBase
{
    public sealed record VisibilityInput(bool Visible);

    [HttpGet("reviews")]
    public async Task<IActionResult> Reviews(CancellationToken ct)
    {
        var items = await db.Reviews.AsNoTracking().OrderByDescending(review => review.Id)
            .Select(review => new
            {
                id = review.Id, customerName = review.CustomerName, message = review.Message,
                rating = review.Rating, isVisible = review.IsVisible
            }).ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("reviews/{id:int}/visibility"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Visibility(int id, [FromBody] VisibilityInput? input, CancellationToken ct)
    {
        if (input is null) return BadRequest(new { message = "البيانات ناقصة." });
        var review = await db.Reviews.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (review is null) return NotFound();
        review.IsVisible = input.Visible;
        await db.SaveChangesAsync(ct);
        return Ok(new { id = review.Id, isVisible = review.IsVisible });
    }

    [HttpPost("reviews/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (review is null) return NotFound();
        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("contact-messages")]
    public async Task<IActionResult> ContactMessages(CancellationToken ct)
    {
        var items = await db.ContactMessages.AsNoTracking().OrderByDescending(message => message.Id)
            .Select(message => new
            {
                id = message.Id, name = message.Name, email = message.Email, phoneNumber = message.PhoneNumber,
                message = message.Message, receivedAt = message.ReceivedAt
            }).ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("contact-messages/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContactMessage(int id, CancellationToken ct)
    {
        var message = await db.ContactMessages.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (message is null) return NotFound();
        db.ContactMessages.Remove(message);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
