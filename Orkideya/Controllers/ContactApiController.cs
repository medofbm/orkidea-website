using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Controllers;

/// <summary>Public contact form. Needs a phone number or an email so the team can reply.</summary>
[ApiController]
[Route("api/v1/contact")]
public sealed partial class ContactApiController(ApplicationDbContext db) : ControllerBase
{
    public sealed record ContactInput(string? Name, string? Phone, string? Email, string? Message);

    [GeneratedRegex(@"^\+?[0-9]{7,15}$")]
    private static partial Regex PhonePattern();

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Send([FromBody] ContactInput? input, CancellationToken ct)
    {
        var error = Validate(input, out var name, out var phone, out var email, out var message);
        if (error is not null) return BadRequest(new { message = error });
        db.ContactMessages.Add(new ContactMessage
        {
            Name = name, Email = email, PhoneNumber = phone, // column is NOT NULL (no schema change allowed), so "" means none
            Message = message, ReceivedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return Accepted(new { message = "وصلتنا رسالتك، بنرد عليك في أقرب وقت." });
    }

    public static string? Validate(ContactInput? input, out string name, out string phone, out string email, out string message)
    {
        name = input?.Name?.Trim() ?? "";
        phone = input?.Phone?.Trim() ?? "";
        email = input?.Email?.Trim() ?? "";
        message = input?.Message?.Trim() ?? "";
        if (input is null) return "البيانات ناقصة، حاولي مرة ثانية.";
        if (name.Length < 2 || name.Length > 80) return "اكتبي اسمك (من حرفين إلى 80 حرفًا).";
        if (phone.Length == 0 && email.Length == 0) return "اكتبي رقم هاتفك أو بريدك الإلكتروني عشان نقدر نرد عليك.";
        if (phone.Length != 0 && !PhonePattern().IsMatch(phone)) return "رقم الهاتف غير صحيح، استخدمي أرقام فقط (ويقدر يبدأ بـ +).";
        if (email.Length != 0 && (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)))
            return "البريد الإلكتروني غير صحيح.";
        if (message.Length < 10 || message.Length > 1000) return "اكتبي رسالتك بين 10 و1000 حرف.";
        return null;
    }
}
