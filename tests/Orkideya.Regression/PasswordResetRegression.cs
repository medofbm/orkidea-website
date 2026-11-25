using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orkideya.Controllers;
using Orkideya.Services.Email;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

/// <summary>Password reset by email: antiforgery, validation, no account enumeration, email template, token lifespan. No database, no SMTP.</summary>
internal static class PasswordResetRegression
{
    private const string GenericMessage = "إذا كان البريد مسجّل عندنا، بيوصلك رابط لتغيير كلمة المرور خلال دقائق.";
    private const string KnownEmail = "known.customer@example.com";

    private sealed class CapturingSender : IEmailSender
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Sent.Add((email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }

    /// <summary>In-memory user store with one known user; no database.</summary>
    private sealed class FakeUserStore : IUserEmailStore<IdentityUser>, IUserSecurityStampStore<IdentityUser>
    {
        private static readonly IdentityUser Known = new()
        {
            Id = "u1", UserName = KnownEmail, NormalizedUserName = KnownEmail.ToUpperInvariant(),
            Email = KnownEmail, NormalizedEmail = KnownEmail.ToUpperInvariant(), SecurityStamp = "stamp"
        };
        public void Dispose() { }
        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(IdentityUser user, string? name, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult<IdentityUser?>(userId == Known.Id ? Known : null);
        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
            Task.FromResult<IdentityUser?>(normalizedUserName == Known.NormalizedUserName ? Known : null);
        public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.Email);
        public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(false);
        public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
            Task.FromResult<IdentityUser?>(normalizedEmail == Known.NormalizedEmail ? Known : null);
        public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.NormalizedEmail);
        public Task SetNormalizedEmailAsync(IdentityUser user, string? email, CancellationToken ct) => Task.CompletedTask;
        public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);
    }

    private static WebApplicationFactory<CartController> Create(CapturingSender sender) =>
        new WebApplicationFactory<CartController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Initialization:Run"] = "false",
                    ["AllowedHosts"] = "localhost",
                    ["App:PublicBaseUrl"] = "https://shop.example.test",
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IUserStore<IdentityUser>>();
                services.AddSingleton<IUserStore<IdentityUser>, FakeUserStore>();
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            });
        });

    private static HttpClient Client(WebApplicationFactory<CartController> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });

    private static async Task<HttpRequestMessage> Post(HttpClient client, string path, object body)
    {
        using var session = await client.GetAsync("/api/v1/session");
        using var json = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", json.RootElement.GetProperty("requestToken").GetString());
        return request;
    }

    private static async Task<string?> Message(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
    }

    public static async Task Run(Action<bool, string> check)
    {
        // Template: link present once inside the button, token appears nowhere else.
        const string fakeToken = "TOKEN_abc-123";
        var link = PasswordResetEmail.BuildLink("https://shop.example.test/", "a@b.com", fakeToken);
        var html = PasswordResetEmail.BuildHtml(link);
        check(link == "https://shop.example.test/account/reset-password?email=a%40b.com&token=TOKEN_abc-123", "Reset link format");
        check(html.Contains("dir=\"rtl\"") && html.Contains("#751D82") && html.Contains("#4E1258"), "Reset email is RTL with logo colours");
        var authFailure = new System.Net.Mail.SmtpException(System.Net.Mail.SmtpStatusCode.ClientNotPermitted, "secret@example.test");
        check(SmtpEmailSender.DescribeStatus(authFailure) == "ClientNotPermitted (454)" && !SmtpEmailSender.DescribeStatus(authFailure).Contains("@"), "SMTP failure log shows the status code only");
        var network = new System.Net.Mail.SmtpException("x", new System.IO.IOException("y", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.TimedOut)));
        check(SmtpEmailSender.DescribeCause(network) == "SocketException:TimedOut", "SMTP failure log names the network cause");
        check(html.Contains("href=\"" + System.Net.WebUtility.HtmlEncode(link) + "\""), "Reset email contains the link");
        check(html.Split(fakeToken).Length == 2, "Raw token appears only inside the link");
        check(html.Contains("ساعتين") && html.Contains("إذا ما طلبتيش، تجاهلي الرسالة."), "Reset email has expiry and ignore note");
        var plain = SmtpEmailSender.ToPlainText(html);
        check(plain.Contains(link) && !plain.Contains("<"), "Plain-text alternative contains the link and no markup");

        // Error mapping.
        check(AccountController.ArabicError("InvalidToken") == "الرابط غير صالح أو انتهت صلاحيته. اطلبي رابط جديد.", "InvalidToken maps to Arabic message");
        check(new[] { "PasswordTooShort", "PasswordRequiresDigit", "PasswordRequiresUpper", "PasswordRequiresLower", "PasswordRequiresNonAlphanumeric" }
            .Select(AccountController.ArabicError).Distinct().Count() == 5, "Password rule codes map to distinct Arabic messages");

        var sender = new CapturingSender();
        using (var factory = Create(sender))
        {
            var lifespan = factory.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan;
            check(lifespan == TimeSpan.FromHours(2), "Reset token lifespan is 2 hours");
            check(factory.Services.GetRequiredService<IEmailSender>() is CapturingSender, "Test sender replaces the production sender");

            using var client = Client(factory);
            using var noToken = await client.PostAsJsonAsync("/api/v1/account/forgot-password", new { email = "x@example.com" });
            check(noToken.StatusCode == HttpStatusCode.BadRequest, "Forgot-password without antiforgery rejected");
            using var noTokenReset = await client.PostAsJsonAsync("/api/v1/account/reset-password",
                new { email = "x@example.com", token = "x", password = "Aa1!aaaa", confirmPassword = "Aa1!aaaa" });
            check(noTokenReset.StatusCode == HttpStatusCode.BadRequest, "Reset-password without antiforgery rejected");

            using var badEmail = await Post(client, "/api/v1/account/forgot-password", new { email = "not-an-email" });
            using var badEmailResponse = await client.SendAsync(badEmail);
            check(badEmailResponse.StatusCode == HttpStatusCode.BadRequest, "Forgot-password with invalid email format returns 400");

        }
        var sender1b = new CapturingSender();
        using (var factory = Create(sender1b))
        using (var client = Client(factory))
        {
            using var unknown = await Post(client, "/api/v1/account/forgot-password", new { email = "nobody@example.com" });
            using var unknownResponse = await client.SendAsync(unknown);
            check(unknownResponse.StatusCode == HttpStatusCode.Accepted && await Message(unknownResponse) == GenericMessage,
                "Unknown email returns 202 with the generic message");
            check(sender1b.Sent.Count == 0, "Unknown email sends nothing");
        }

        var sender2 = new CapturingSender();
        using (var factory = Create(sender2))
        using (var client = Client(factory))
        {
            using var known = await Post(client, "/api/v1/account/forgot-password", new { email = KnownEmail });
            using var knownResponse = await client.SendAsync(known);
            check(knownResponse.StatusCode == HttpStatusCode.Accepted && await Message(knownResponse) == GenericMessage,
                "Known email returns the same 202 message");
            check(sender2.Sent.Count == 1 && sender2.Sent[0].To == KnownEmail && sender2.Sent[0].Subject == PasswordResetEmail.Subject,
                "Known email sends one reset message with the Arabic subject");

            var sentHtml = sender2.Sent[0].Html;
            var start = sentHtml.IndexOf("token=", StringComparison.Ordinal) + 6;
            var end = sentHtml.IndexOf('"', start);
            var encodedToken = sentHtml[start..end];
            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            check(sentHtml.Split(encodedToken).Length == 2 && !sentHtml.Contains(token), "Encoded token appears once, inside the link, and the raw token never");
            check(sentHtml.Contains("https://shop.example.test/account/reset-password?email=known.customer%40example.com&amp;token="), "Link uses the configured public base URL");

            using var mismatch = await Post(client, "/api/v1/account/reset-password",
                new { email = KnownEmail, token = encodedToken, password = "Aa1!aaaa", confirmPassword = "Different1!" });
            using var mismatchResponse = await client.SendAsync(mismatch);
            check(mismatchResponse.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(await Message(mismatchResponse)),
                "Reset with mismatched passwords returns 400");

            using var badToken = await Post(client, "/api/v1/account/reset-password",
                new { email = KnownEmail, token = "***not-base64***", password = "Aa1!aaaa", confirmPassword = "Aa1!aaaa" });
            using var badTokenResponse = await client.SendAsync(badToken);
            check(badTokenResponse.StatusCode == HttpStatusCode.BadRequest &&
                await Message(badTokenResponse) == "الرابط غير صالح أو انتهت صلاحيته. اطلبي رابط جديد.", "Malformed token returns the invalid-link message");
        }
    }
}
