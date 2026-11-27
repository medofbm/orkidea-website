using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orkideya.Application.Accounts;

namespace Orkideya.Controllers;

public sealed record PostOrderAccountInput([Required, EmailAddress, StringLength(254)] string Email);

[ApiController]
[Route("api/v1/checkout/receipt/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PostOrderAccountController(PostOrderAccountService service) : ControllerBase
{
    /// <summary>
    /// Receipt card "follow your order in one place". The order comes from the checkout token kept in this browser
    /// session (same access as GET /api/v1/checkout/receipt); the client sends only an email. Always the same answer.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("post-order-account")]
    public async Task<IActionResult> Create(PostOrderAccountInput input)
    {
        if (User.Identity?.IsAuthenticated != true &&
            Guid.TryParse(HttpContext.Session.GetString("CompletedCheckoutToken"), out var token))
            await service.RequestAsync(token, input.Email, HttpContext.RequestAborted);
        return Accepted(new { message = PostOrderAccountService.GenericMessage });
    }
}
