using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Orkideya.Controllers;

public sealed record AccountLogin(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(1024)] string Password,
    bool RememberMe);

public sealed record AccountVerification([Required, StringLength(64)] string Code, bool Recovery, bool RememberMe);

[ApiController]
[Route("api/v1/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(SignInManager<IdentityUser> signInManager) : ControllerBase
{
    [HttpGet]
    public IActionResult Current() => Ok(new
    {
        signedIn = User.Identity?.IsAuthenticated == true,
        name = User.Identity?.Name,
        isAdmin = User.IsInRole("Admin")
    });

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AccountLogin input)
    {
        var result = await signInManager.PasswordSignInAsync(
            input.Email.Trim(), input.Password, input.RememberMe, lockoutOnFailure: true);
        if (result.RequiresTwoFactor) return Ok(new { requiresVerification = true });
        return LoginResponse(result);
    }

    [HttpPost("verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(AccountVerification input)
    {
        var code = input.Code.Trim().Replace(" ", "", StringComparison.Ordinal);
        var result = input.Recovery
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(
                code.Replace("-", "", StringComparison.Ordinal), input.RememberMe, rememberClient: false);
        return LoginResponse(result);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    private IActionResult LoginResponse(Microsoft.AspNetCore.Identity.SignInResult result)
    {
        if (result.Succeeded) return Ok(new { requiresVerification = false });
        return Unauthorized(new { message = "تعذر تسجيل الدخول. راجع البيانات وحاول مرة أخرى." });
    }
}
