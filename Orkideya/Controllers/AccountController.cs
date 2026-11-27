using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Text;
using Orkideya.Application.Accounts;
using Orkideya.Services.Email;

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

public sealed record ForgotPasswordInput([Required, EmailAddress, StringLength(254)] string Email);

public sealed record ResetPasswordInput(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(2048)] string Token,
    [Required, StringLength(100)] string Password,
    [Required, StringLength(100)] string ConfirmPassword);

[ApiController]
[Route("api/v1/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager, IEmailSender emailSender, IOptions<AppOptions> appOptions,
    IAccountActivityRecorder activity, ILogger<AccountController> logger) : ControllerBase
{
    private const string InvalidTokenMessage = "الرابط غير صالح أو انتهت صلاحيته. اطلبي رابط جديد.";

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account-recovery")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordInput input)
    {
        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        var baseUrl = appOptions.Value.PublicBaseUrl;
        // Accounts use the email as the login name, so any user found by email may receive the link.
        if (user?.Email is { Length: > 0 } address)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                logger.LogWarning("App:PublicBaseUrl is empty; password reset email not sent.");
            else
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var link = PasswordResetEmail.BuildLink(baseUrl, address, encoded);
                await emailSender.SendEmailAsync(address, PasswordResetEmail.Subject, PasswordResetEmail.BuildHtml(link));
            }
        }
        // Same answer whether or not the account exists (no account enumeration).
        return Accepted(new { message = "إذا كان البريد مسجّل عندنا، بيوصلك رابط لتغيير كلمة المرور خلال دقائق." });
    }

    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account-recovery")]
    public async Task<IActionResult> ResetPassword(ResetPasswordInput input)
    {
        if (input.Password != input.ConfirmPassword)
            return BadRequest(new { message = "كلمتا المرور غير متطابقتين." });

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Token)); }
        catch (FormatException) { return BadRequest(new { message = InvalidTokenMessage }); }

        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        if (user is null) return BadRequest(new { message = InvalidTokenMessage });

        var result = await userManager.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Code).Distinct().Select(ArabicError).ToArray();
            var message = result.Errors.Any(e => e.Code == "InvalidToken") ? InvalidTokenMessage : "كلمة المرور لا تحقق الشروط.";
            return BadRequest(new { message, errors });
        }
        // The reset link was emailed to this address, so using it proves the address (accounts made after ordering start unconfirmed).
        if (!user.EmailConfirmed) { user.EmailConfirmed = true; await userManager.UpdateAsync(user); }
        return Ok(new { message = "تم تغيير كلمة المرور. تقدري تسجلي دخولك الحين." });
    }

    public static string ArabicError(string code) => code switch
    {
        "PasswordTooShort" => "كلمة المرور قصيرة (8 أحرف على الأقل).",
        "PasswordRequiresDigit" => "لازم تحتوي على رقم واحد على الأقل.",
        "PasswordRequiresUpper" => "لازم تحتوي على حرف إنجليزي كبير.",
        "PasswordRequiresLower" => "لازم تحتوي على حرف إنجليزي صغير.",
        "PasswordRequiresNonAlphanumeric" => "لازم تحتوي على رمز خاص واحد على الأقل.",
        "InvalidToken" => InvalidTokenMessage,
        _ => "كلمة المرور غير مقبولة."
    };

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
    [EnableRateLimiting("account-sign-up")]
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
        await activity.RecordCreatedAsync(user.Id, signedIn: true, HttpContext.RequestAborted);
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
    [EnableRateLimiting("account-sign-in")]
    public async Task<IActionResult> Login(AccountLogin input)
    {
        var result = await signInManager.PasswordSignInAsync(
            input.Email.Trim(), input.Password, input.RememberMe, lockoutOnFailure: true);
        if (result.RequiresTwoFactor) return Ok(new { requiresVerification = true });
        if (result.Succeeded && await userManager.FindByNameAsync(input.Email.Trim()) is { } user)
            await activity.RecordSignInAsync(user.Id, HttpContext.RequestAborted);
        return LoginResponse(result);
    }

    [HttpPost("verify")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account-sign-in")]
    public async Task<IActionResult> Verify(AccountVerification input)
    {
        var code = input.Code.Trim().Replace(" ", "", StringComparison.Ordinal);
        // Read before signing in: the two-factor cookie that names the user is cleared on success.
        var pending = await signInManager.GetTwoFactorAuthenticationUserAsync();
        var result = input.Recovery
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(
                code.Replace("-", "", StringComparison.Ordinal), input.RememberMe, rememberClient: false);
        if (result.Succeeded && pending is not null)
            await activity.RecordSignInAsync(pending.Id, HttpContext.RequestAborted);
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
