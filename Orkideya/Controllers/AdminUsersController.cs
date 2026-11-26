using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orkideya.Application.Accounts;
using Orkideya.Data;
using Orkideya.Services.Email;

namespace Orkideya.Controllers;

/// <summary>Email only. There is deliberately no password field: a new admin sets their own password by email.</summary>
public sealed record AddAdminInput([Required, EmailAddress, StringLength(254)] string Email);

/// <summary>
/// Admin pages «الزبائن» and «المديرين». Every admin can read; only the super admin (Admin:SuperAdminEmail) can add or
/// remove administrators. The super admin can never be removed, and nobody can remove themselves.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUsersController(ApplicationDbContext db, UserManager<IdentityUser> userManager,
    IConfiguration configuration, IEmailSender emailSender, IOptions<AppOptions> appOptions,
    IAccountActivityRecorder activity, ILogger<AdminUsersController> logger) : ControllerBase
{
    public const int PageSize = 25;
    private const string AdminRoleNormalized = "ADMIN";

    private string? SuperAdminEmail => configuration[AdminAccountRules.SuperAdminEmailKey];

    private async Task<(IdentityUser? User, bool IsSuperAdmin)> CallerAsync()
    {
        var caller = await userManager.GetUserAsync(User);
        return (caller, caller is not null && AdminAccountRules.IsSuperAdminEmail(caller.Email, SuperAdminEmail));
    }

    /// <summary>Accounts (customers and admins), newest first, 25 per page. Search by email or phone.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct, string search = "", int page = 1, string role = "all")
    {
        search = (search ?? "").Trim();
        if (page < 1 || search.Length > 100 || role is not ("all" or "customers" or "admins"))
            return BadRequest(new { message = "بيانات البحث أو رقم الصفحة غير صالح." });

        var (_, isSuperAdmin) = await CallerAsync();
        var adminRoleId = await db.Roles.Where(r => r.NormalizedName == AdminRoleNormalized).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var admins = db.UserRoles.Where(link => link.RoleId == adminRoleId).Select(link => link.UserId);

        var users = db.Users.AsNoTracking();
        if (search.Length > 0)
            users = users.Where(user => (user.Email != null && user.Email.Contains(search)) ||
                (user.PhoneNumber != null && user.PhoneNumber.Contains(search)));
        if (role == "admins") users = users.Where(user => admins.Contains(user.Id));
        else if (role == "customers") users = users.Where(user => !admins.Contains(user.Id));

        var total = await users.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var rows = await Rows(users, admins)
            .OrderByDescending(row => row.CreatedAtUtc).ThenBy(row => row.Email)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);

        return Ok(new
        {
            items = rows.Select(row => Item(row)),
            page, pages, total, pageSize = PageSize,
            isSuperAdmin
        });
    }

    /// <summary>One account with its orders (newest first).</summary>
    [HttpGet("users/{id}")]
    public async Task<IActionResult> UserDetails(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 450) return NotFound();
        var (_, isSuperAdmin) = await CallerAsync();
        var adminRoleId = await db.Roles.Where(r => r.NormalizedName == AdminRoleNormalized).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var admins = db.UserRoles.Where(link => link.RoleId == adminRoleId).Select(link => link.UserId);
        var row = await Rows(db.Users.AsNoTracking().Where(user => user.Id == id), admins).FirstOrDefaultAsync(ct);
        if (row is null) return NotFound();

        var orders = await db.Orders.AsNoTracking().Where(order => order.CustomerUserId == id)
            .OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId).Take(200)
            .Select(order => new { order.OrderId, order.OrderDate, order.Status, order.TotalAmount }).ToListAsync(ct);

        return Ok(new
        {
            user = Item(row),
            orders = orders.Select(order => new
            {
                id = order.OrderId, orderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc),
                order.Status, total = Money(order.TotalAmount)
            }),
            isSuperAdmin
        });
    }

    /// <summary>All administrators. <c>isSuperAdmin</c> tells the page whether the caller may add or remove admins.</summary>
    [HttpGet("admins")]
    public async Task<IActionResult> Admins(CancellationToken ct)
    {
        var (caller, isSuperAdmin) = await CallerAsync();
        var adminRoleId = await db.Roles.Where(r => r.NormalizedName == AdminRoleNormalized).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var admins = db.UserRoles.Where(link => link.RoleId == adminRoleId).Select(link => link.UserId);
        var rows = await Rows(db.Users.AsNoTracking().Where(user => admins.Contains(user.Id)), admins)
            .OrderBy(row => row.Email).ToListAsync(ct);
        return Ok(new
        {
            items = rows.Select(row => new
            {
                row.Id, row.Email, row.EmailConfirmed,
                isSuperAdmin = AdminAccountRules.IsSuperAdminEmail(row.Email, SuperAdminEmail),
                isYou = caller is not null && caller.Id == row.Id,
                hasPassword = row.HasPassword,
                createdAtUtc = Utc(row.CreatedAtUtc), lastSignInAtUtc = Utc(row.LastSignInAtUtc)
            }),
            isSuperAdmin,
            superAdminConfigured = !string.IsNullOrWhiteSpace(SuperAdminEmail)
        });
    }

    /// <summary>
    /// Super admin only. Existing account: added to the Admin role. No account: one is created without a password and
    /// the usual password-reset email is sent so the new admin chooses their own password.
    /// </summary>
    [HttpPost("admins"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdmin([FromBody] AddAdminInput? input, CancellationToken ct)
    {
        var (caller, isSuperAdmin) = await CallerAsync();
        if (caller is null || !isSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = AdminAccountRules.NotSuperAdminMessage });
        if (input is null || string.IsNullOrWhiteSpace(input.Email))
            return BadRequest(new { message = "اكتب البريد الإلكتروني." });

        var email = input.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        var created = false;
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user); // no password: they set it from the email link
            if (!result.Succeeded)
                return BadRequest(new { message = "تعذر إنشاء الحساب. راجع البريد الإلكتروني." });
            created = true;
        }
        else
        {
            if (await userManager.IsInRoleAsync(user, AdminAccountRules.AdminRole))
                return Conflict(new { message = "هذا الحساب مدير من قبل." });
            if (!user.EmailConfirmed && !await SecureUnconfirmedAccountAsync(user))
                return BadRequest(new { message = "تعذر تجهيز الحساب. حاول مرة أخرى." });
        }

        var added = await userManager.AddToRoleAsync(user, AdminAccountRules.AdminRole);
        if (!added.Succeeded)
            return BadRequest(new { message = "تعذر إضافة المدير. حاول مرة أخرى." });
        if (created) await activity.RecordCreatedAsync(user.Id, signedIn: false, ct);

        // A new account, or one whose password was cleared above, needs a password; send the reset email.
        var needsPassword = !await userManager.HasPasswordAsync(user);
        var emailSent = needsPassword && await SendPasswordEmailAsync(user);
        string message;
        if (!needsPassword)
            message = "تمت إضافة المدير. يسجل خروج ودخول من جديد باش تظهر له لوحة الإدارة.";
        else if (emailSent)
            message = "تمت إضافة المدير وأرسلنا له رابط لاختيار كلمة المرور (صالح لمدة ساعتين).";
        else
            message = "تمت إضافة المدير، لكن ما قدرناش نرسل البريد. يقدر يستعمل «نسيت كلمة المرور» من صفحة الدخول.";
        return Ok(new { id = user.Id, email = user.Email, created, emailSent, message });
    }

    /// <summary>Super admin only. Removes the Admin role (the account itself stays, with its orders).</summary>
    [HttpPost("admins/{id}/remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(string id, CancellationToken ct)
    {
        var (caller, isSuperAdmin) = await CallerAsync();
        if (caller is null || !isSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = AdminAccountRules.NotSuperAdminMessage });
        if (string.IsNullOrWhiteSpace(id) || id.Length > 450) return NotFound();
        var target = await userManager.FindByIdAsync(id);
        if (target is null) return NotFound(new { message = "الحساب غير موجود." });

        var isAdmin = await userManager.IsInRoleAsync(target, AdminAccountRules.AdminRole);
        var refusal = AdminAccountRules.RemovalRefusal(caller.Id, target.Id, target.Email, isAdmin, SuperAdminEmail);
        if (refusal is not null) return BadRequest(new { message = refusal });

        var result = await userManager.RemoveFromRoleAsync(target, AdminAccountRules.AdminRole);
        if (!result.Succeeded) return BadRequest(new { message = "تعذر حذف المدير. حاول مرة أخرى." });
        // New security stamp: their admin sign-in is dropped at the next cookie check.
        await userManager.UpdateSecurityStampAsync(target);
        return Ok(new { id = target.Id, removed = true, message = "تم حذف المدير. الحساب نفسه باقي كحساب زبونة." });
    }

    /// <summary>
    /// An unconfirmed account may have been registered by someone else with this email. Before it becomes an admin,
    /// its password is cleared and its sessions dropped, so only the real owner of the mailbox can set a password.
    /// </summary>
    private async Task<bool> SecureUnconfirmedAccountAsync(IdentityUser user)
    {
        if (await userManager.HasPasswordAsync(user) && !(await userManager.RemovePasswordAsync(user)).Succeeded) return false;
        if (userManager.SupportsUserTwoFactor && await userManager.GetTwoFactorEnabledAsync(user) &&
            !(await userManager.SetTwoFactorEnabledAsync(user, false)).Succeeded) return false;
        user.EmailConfirmed = true;
        if (!(await userManager.UpdateAsync(user)).Succeeded) return false;
        return (await userManager.UpdateSecurityStampAsync(user)).Succeeded;
    }

    /// <summary>Same email as «نسيت كلمة المرور» (same template, same 2-hour token).</summary>
    private async Task<bool> SendPasswordEmailAsync(IdentityUser user)
    {
        var baseUrl = appOptions.Value.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(user.Email))
        {
            logger.LogWarning("App:PublicBaseUrl is empty; new admin password email not sent.");
            return false;
        }
        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = PasswordResetEmail.BuildLink(baseUrl, user.Email, encoded);
            await emailSender.SendEmailAsync(user.Email, PasswordResetEmail.Subject, PasswordResetEmail.BuildHtml(link));
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("New admin password email failed: {Error}", exception.GetType().Name);
            return false;
        }
    }

    private sealed class UserRow
    {
        public string Id { get; init; } = "";
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public bool EmailConfirmed { get; init; }
        public bool HasPassword { get; init; }
        public DateTime? CreatedAtUtc { get; init; }
        public DateTime? LastSignInAtUtc { get; init; }
        public int? SignInCount { get; init; }
        public bool IsAdmin { get; init; }
        public int OrderCount { get; init; }
        public decimal TotalSpent { get; init; }
    }

    // Member-init projection (not a constructor) so EF can sort and page on the projected columns.
    private IQueryable<UserRow> Rows(IQueryable<IdentityUser> users, IQueryable<string> admins) =>
        from user in users
        join row in db.AccountActivities on user.Id equals row.UserId into activities
        from activity in activities.DefaultIfEmpty()
        select new UserRow
        {
            Id = user.Id, Email = user.Email, PhoneNumber = user.PhoneNumber, EmailConfirmed = user.EmailConfirmed,
            HasPassword = user.PasswordHash != null,
            CreatedAtUtc = activity == null ? null : activity.CreatedAtUtc,
            LastSignInAtUtc = activity == null ? null : activity.LastSignInAtUtc,
            SignInCount = activity == null ? null : (int?)activity.SignInCount,
            IsAdmin = admins.Contains(user.Id),
            OrderCount = db.Orders.Count(order => order.CustomerUserId == user.Id),
            // Cancelled orders are not money spent.
            TotalSpent = db.Orders.Where(order => order.CustomerUserId == user.Id && order.Status != "Cancelled")
                .Sum(order => (decimal?)order.TotalAmount) ?? 0m
        };

    private object Item(UserRow row) => new
    {
        row.Id, row.Email, row.PhoneNumber, row.EmailConfirmed,
        createdAtUtc = Utc(row.CreatedAtUtc), lastSignInAtUtc = Utc(row.LastSignInAtUtc), row.SignInCount,
        row.IsAdmin, isSuperAdmin = row.IsAdmin && AdminAccountRules.IsSuperAdminEmail(row.Email, SuperAdminEmail),
        row.OrderCount, totalSpent = Money(row.TotalSpent)
    };

    private static DateTime? Utc(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
