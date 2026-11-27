using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orkideya.Data;
using Orkideya.Services.Email;

namespace Orkideya.Application.Accounts;

/// <summary>Order side of "create an account after ordering"; a fake replaces it in tests (no database).</summary>
public interface IOrderAccountLinks
{
    /// <summary>True when an order with this checkout token exists, has no customer account and is not older than the cut-off.</summary>
    Task<bool> CanLinkAsync(Guid checkoutToken, DateTime notBeforeUtc, CancellationToken cancel);

    /// <summary>Links that order to the user. One atomic update, so a second call (or a race) links nothing.</summary>
    Task<bool> TryLinkAsync(Guid checkoutToken, string userId, DateTime notBeforeUtc, CancellationToken cancel);
}

public sealed class OrderAccountLinks(ApplicationDbContext context) : IOrderAccountLinks
{
    public Task<bool> CanLinkAsync(Guid checkoutToken, DateTime notBeforeUtc, CancellationToken cancel) =>
        context.Orders.AsNoTracking().AnyAsync(order =>
            order.CheckoutToken == checkoutToken && order.CustomerUserId == null && order.OrderDate >= notBeforeUtc, cancel);

    public async Task<bool> TryLinkAsync(Guid checkoutToken, string userId, DateTime notBeforeUtc, CancellationToken cancel) =>
        await context.Orders.Where(order =>
                order.CheckoutToken == checkoutToken && order.CustomerUserId == null && order.OrderDate >= notBeforeUtc)
            .ExecuteUpdateAsync(set => set.SetProperty(order => order.CustomerUserId, userId), cancel) == 1;
}

/// <summary>
/// A guest who just ordered gives an email and gets a set-password link. The checkout token (known only to the browser
/// session that placed the order) is the proof of ownership; phone numbers are never used. Callers must answer every
/// request with the same message, so nothing here reveals whether an email or token was valid.
/// </summary>
public sealed class PostOrderAccountService(IOrderAccountLinks links, UserManager<IdentityUser> users, IEmailSender mail,
    IOptions<AppOptions> app, IConfiguration configuration, TimeProvider clock, ILogger<PostOrderAccountService> logger)
{
    public const string GenericMessage = "لو الإيميل صحيح، بيوصلك رابط خلال دقايق.";
    public static readonly TimeSpan OrderWindow = TimeSpan.FromDays(7);

    public async Task RequestAsync(Guid checkoutToken, string? rawEmail, CancellationToken cancel)
    {
        var email = rawEmail?.Trim();
        if (checkoutToken == Guid.Empty || string.IsNullOrEmpty(email) || email.Length > 254 ||
            !new EmailAddressAttribute().IsValid(email)) return;
        // The store owner's address is never created or linked from a public page.
        if (AdminAccountRules.IsSuperAdminEmail(email, configuration[AdminAccountRules.SuperAdminEmailKey])) return;
        var baseUrl = app.Value.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("App:PublicBaseUrl is empty; post-order account email not sent.");
            return;
        }

        var notBefore = clock.GetUtcNow().UtcDateTime - OrderWindow;
        if (!await links.CanLinkAsync(checkoutToken, notBefore, cancel)) return;

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Never attach an order to an account that already exists: whoever typed the address may not own it.
            if (await users.IsInRoleAsync(existing, AdminAccountRules.AdminRole)) return;
            await SendAsync(email, PostOrderAccountEmail.ExistingAccountSubject,
                PostOrderAccountEmail.BuildExistingAccountHtml(baseUrl.TrimEnd('/') + "/account"));
            return;
        }

        // No password and no role, like a normal customer account. The email is confirmed when the link below is used.
        var user = new IdentityUser { UserName = email, Email = email };
        if (!(await users.CreateAsync(user)).Succeeded) return;
        if (!await links.TryLinkAsync(checkoutToken, user.Id, notBefore, cancel)) return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = PasswordResetEmail.BuildLink(baseUrl, email, WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));
        await SendAsync(email, PostOrderAccountEmail.SetPasswordSubject, PostOrderAccountEmail.BuildSetPasswordHtml(link));
    }

    private async Task SendAsync(string to, string subject, string html)
    {
        // A mail failure must not change the answer the visitor sees.
        try { await mail.SendEmailAsync(to, subject, html); }
        catch (Exception error) { logger.LogWarning("Post-order account email failed: {Error}", error.GetType().Name); }
    }
}
