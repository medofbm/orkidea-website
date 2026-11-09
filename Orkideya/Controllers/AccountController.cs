using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Orkideya.Controllers;

public sealed record AccountLogin(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(1024)] string Password,
    bool RememberMe);

public sealed record AccountVerification([Required, StringLength(64)] string Code, bool Recovery, bool RememberMe);

public sealed record AccountRegistration(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    [Required, StringLength(100)] string Confirmation);

public sealed record AccountPhone([Phone, StringLength(32)] string? PhoneNumber);

public sealed record AccountPassword(
    [Required, StringLength(1024)] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword,
    [Required, StringLength(100)] string Confirmation);

[ApiController]
[Route("api/v1/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Current()
    {
        var user = await userManager.GetUserAsync(User);
        return Ok(new
        {
            signedIn = user is not null,
            name = user?.UserName,
            phoneNumber = user?.PhoneNumber,
            isAdmin = user is not null && User.IsInRole("Admin")
        });
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(AccountRegistration input)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Conflict(new { message = "سجّل الخروج قبل إنشاء حساب جديد." });

        var options = userManager.Options.SignIn;
        if (options.RequireConfirmedAccount || options.RequireConfirmedEmail || options.RequireConfirmedPhoneNumber)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "إنشاء الحساب من هذه الصفحة غير متاح حاليًا." });

        if (input.Password != input.Confirmation)
            return BadRequest(new { message = "كلمتا المرور غير متطابقتين." });

        var email = input.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "تعذر إنشاء الحساب. راجع البريد ومتطلبات كلمة المرور أو جرّب تسجيل الدخول." });

        await signInManager.SignInAsync(user, isPersistent: false);
        return NoContent();
    }

    [Authorize]
    [HttpPost("phone")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Phone(AccountPhone input)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        if (phone == user.PhoneNumber) return NoContent();
        var result = await userManager.SetPhoneNumberAsync(user, phone);
        if (!result.Succeeded)
            return BadRequest(new { message = "تعذر تحديث رقم الهاتف. أعد تحميل الحساب وحاول مرة أخرى." });
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    [Authorize]
    [HttpPost("password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(AccountPassword input)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (input.NewPassword != input.Confirmation)
            return BadRequest(new { message = "كلمتا المرور غير متطابقتين." });
        var result = await userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = "تعذر تغيير كلمة المرور. راجع كلمة المرور الحالية ومتطلبات الجديدة." });
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

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
