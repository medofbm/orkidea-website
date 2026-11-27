using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orkideya.Application.Accounts;
using Orkideya.Controllers;
using Orkideya.Services.Email;

/// <summary>
/// "Create an account after ordering": links the order only for a new email, never for an existing account, an already
/// linked, old or unknown order, or an admin address. In-memory users and orders (a fake IOrderAccountLinks), no
/// database, no SMTP. All addresses are synthetic (example.test).
/// </summary>
internal static class PostOrderAccountRegression
{
    private const string OwnerEmail = "owner@example.test";
    private const string HelperEmail = "helper@example.test";
    private const string ExistingEmail = "existing@example.test";
    private const string NewEmail = "new.customer@example.test";

    private sealed class CapturingSender : IEmailSender
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Sent.Add((email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }

    /// <summary>Orders by checkout token: owner user id (null = guest) and order date. Same rules as the SQL version.</summary>
    private sealed class FakeLinks : IOrderAccountLinks
    {
        public Dictionary<Guid, (string? UserId, DateTime Date)> Orders { get; } = new();
        public Task<bool> CanLinkAsync(Guid token, DateTime notBeforeUtc, CancellationToken cancel) =>
            Task.FromResult(Orders.TryGetValue(token, out var order) && order.UserId is null && order.Date >= notBeforeUtc);
        public async Task<bool> TryLinkAsync(Guid token, string userId, DateTime notBeforeUtc, CancellationToken cancel)
        {
            if (!await CanLinkAsync(token, notBeforeUtc, cancel)) return false;
            Orders[token] = (userId, Orders[token].Date);
            return true;
        }
    }

    public static async Task Run(Action<bool, string> check)
    {
        check(PostOrderAccountService.GenericMessage == "لو الإيميل صحيح، بيوصلك رابط خلال دقايق.", "Generic message is the agreed Arabic text");
        check(PostOrderAccountEmail.BuildSetPasswordHtml("https://x.test/a?b=1&c=2").Contains("href=\"https://x.test/a?b=1&amp;c=2\"") &&
            PostOrderAccountEmail.BuildExistingAccountHtml("https://x.test/account").Contains("dir=\"rtl\""), "Account emails are RTL and encode the link");

        var store = new AdminUsersRegression.MemoryUserStore();
        store.Add(new IdentityUser { Id = "helper-id", UserName = HelperEmail, Email = HelperEmail, EmailConfirmed = true }, "Admin");
        store.Add(new IdentityUser { Id = "existing-id", UserName = ExistingEmail, Email = ExistingEmail });
        var sender = new CapturingSender();
        var links = new FakeLinks();
        var fresh = Guid.NewGuid(); var other = Guid.NewGuid(); var linked = Guid.NewGuid(); var old = Guid.NewGuid();
        var forExisting = Guid.NewGuid(); var forOwner = Guid.NewGuid(); var forHelper = Guid.NewGuid(); var unknown = Guid.NewGuid();
        foreach (var token in new[] { fresh, other, forExisting, forOwner, forHelper }) links.Orders[token] = (null, DateTime.UtcNow.AddMinutes(-5));
        links.Orders[linked] = ("someone-id", DateTime.UtcNow.AddMinutes(-5));
        links.Orders[old] = (null, DateTime.UtcNow.AddDays(-8));

        using var factory = new WebApplicationFactory<CartController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Initialization:Run"] = "false",
                    ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1,1;Database=OrkideaTests;Integrated Security=true;Connect Timeout=1",
                    ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "orkidea-test-keys"),
                    ["AllowedHosts"] = "localhost",
                    ["App:PublicBaseUrl"] = "https://shop.example.test",
                    [AdminAccountRules.SuperAdminEmailKey] = OwnerEmail,
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IUserStore<IdentityUser>>();
                services.AddSingleton<IUserStore<IdentityUser>>(store);
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
                services.RemoveAll<IOrderAccountLinks>();
                services.AddSingleton<IOrderAccountLinks>(links);
            });
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PostOrderAccountService>();
        var usersBefore = store.Users.Count;

        // Wrong or unknown token, and an order that is too old: nothing happens.
        await service.RequestAsync(unknown, "nobody@example.test", default);
        await service.RequestAsync(Guid.Empty, "nobody@example.test", default);
        await service.RequestAsync(old, "late@example.test", default);
        check(sender.Sent.Count == 0 && store.Users.Count == usersBefore && links.Orders[old].UserId is null,
            "Unknown token, empty token and an order older than 7 days do nothing");

        // Already linked order: nothing, and the owner stays the owner.
        await service.RequestAsync(linked, "thief@example.test", default);
        check(sender.Sent.Count == 0 && store.Users.Count == usersBefore && links.Orders[linked].UserId == "someone-id",
            "An already linked order is not linked again");

        // Bad email shapes do nothing.
        await service.RequestAsync(fresh, "not-an-email", default);
        check(sender.Sent.Count == 0 && links.Orders[fresh].UserId is null, "Invalid email does nothing");

        // New email: account created without password, unconfirmed, no role; order linked; one set-password email.
        await service.RequestAsync(fresh, "  " + NewEmail + " ", default);
        var created = store.Users.Values.SingleOrDefault(user => user.Email == NewEmail);
        check(created is not null && links.Orders[fresh].UserId == created.Id, "New email: account created and order linked to it");
        check(created!.PasswordHash is null && !created.EmailConfirmed && store.Roles[created.Id].Count == 0,
            "New account has no password, unconfirmed email and no role");
        check(sender.Sent.Count == 1 && sender.Sent[0].To == NewEmail && sender.Sent[0].Subject == PostOrderAccountEmail.SetPasswordSubject &&
            sender.Sent[0].Html.Contains("https://shop.example.test/account/reset-password?email=new.customer%40example.test&amp;token="),
            "New email: exactly one set-password email using the reset-password page");

        // Second call for the same token (same or another email) does nothing.
        await service.RequestAsync(fresh, NewEmail, default);
        await service.RequestAsync(fresh, "another@example.test", default);
        check(sender.Sent.Count == 1 && links.Orders[fresh].UserId == created.Id && store.Users.Count == usersBefore + 1,
            "Idempotent: a second call for a linked token does nothing");

        // Existing account email: never linked; the address gets a "you already have an account" email.
        var sentBefore = sender.Sent.Count;
        await service.RequestAsync(forExisting, ExistingEmail, default);
        check(links.Orders[forExisting].UserId is null && store.Users.Count == usersBefore + 1, "Existing email: order is not linked and no account is created");
        check(sender.Sent.Count == sentBefore + 1 && sender.Sent[^1].To == ExistingEmail &&
            sender.Sent[^1].Subject == PostOrderAccountEmail.ExistingAccountSubject && !sender.Sent[^1].Html.Contains("token="),
            "Existing email: one login email without any reset token");

        // Admin addresses: the super admin (even with no account) and an existing admin account.
        sentBefore = sender.Sent.Count;
        await service.RequestAsync(forOwner, " OWNER@example.test ", default);
        await service.RequestAsync(forHelper, HelperEmail, default);
        check(links.Orders[forOwner].UserId is null && links.Orders[forHelper].UserId is null &&
            sender.Sent.Count == sentBefore && store.Users.Count == usersBefore + 1 && !store.Users.Values.Any(user => user.Email == OwnerEmail),
            "Admin and super admin emails are never created, linked or emailed");

        // HTTP: antiforgery required, invalid email 400, and without a receipt session the answer is the same generic 202.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var noToken = await client.PostAsJsonAsync("/api/v1/checkout/receipt/account", new { email = NewEmail });
        check(noToken.StatusCode == HttpStatusCode.BadRequest, "Account card POST without antiforgery is rejected");
        async Task<HttpResponseMessage> PostWithToken(string path, object body)
        {
            using var session = await client.GetAsync("/api/v1/session");
            using var json = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("RequestVerificationToken", json.RootElement.GetProperty("requestToken").GetString());
            return await client.SendAsync(request);
        }
        using var invalid = await PostWithToken("/api/v1/checkout/receipt/account", new { email = "nope" });
        check(invalid.StatusCode == HttpStatusCode.BadRequest, "Account card POST with an invalid email returns 400");
        sentBefore = sender.Sent.Count;
        foreach (var email in new[] { NewEmail, ExistingEmail, OwnerEmail })
        {
            using var response = await PostWithToken("/api/v1/checkout/receipt/account", new { email });
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.Accepted && body.RootElement.GetProperty("message").GetString() == PostOrderAccountService.GenericMessage,
                "Same generic 202 answer without a receipt session: " + email);
        }
        check(sender.Sent.Count == sentBefore, "No receipt session means no email");

        // Setting the password through the emailed link confirms the address.
        var html = sender.Sent[0].Html;
        var start = html.IndexOf("token=", StringComparison.Ordinal) + 6;
        var encoded = html[start..html.IndexOf('"', start)];
        using var reset = await PostWithToken("/api/v1/account/reset-password",
            new { email = NewEmail, token = encoded, password = "Aa1!aaaa", confirmPassword = "Aa1!aaaa" });
        check(reset.StatusCode == HttpStatusCode.OK && created.EmailConfirmed && created.PasswordHash is not null,
            "Setting the password from the emailed link confirms the email");
    }
}
