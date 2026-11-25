using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkideya.Controllers;
using Orkideya.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>Public reviews/contact and admin feedback API: routes, antiforgery, roles, validation. No database access.</summary>
internal static class FeedbackApiRegression
{
    private static WebApplicationFactory<CartController> Create() =>
        new WebApplicationFactory<CartController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Initialization:Run"] = "false",
                    ["Notifications:WhatsAppEnabled"] = "false",
                    ["AllowedHosts"] = "localhost",
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddDbContext<ApplicationDbContext>(options => options
                    .UseSqlServer("Server=127.0.0.1,1;Database=OrkideaFeedbackTests;Integrated Security=true;Connect Timeout=1")
                    .AddInterceptors(new RejectDatabaseConnections()));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "FeedbackTest";
                    options.DefaultChallengeScheme = "FeedbackTest";
                    options.DefaultForbidScheme = "FeedbackTest";
                }).AddScheme<AuthenticationSchemeOptions, TestRoleHandler>("FeedbackTest", _ => { });
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
        // Pure validation rules (no HTTP).
        check(ReviewsApiController.Validate(new("Nora", 5, "خدمة ممتازة وتوصيل سريع"), out _, out _) is null, "Valid review accepted");
        check(ReviewsApiController.Validate(new("Nora", 0, "خدمة ممتازة وتوصيل سريع"), out _, out _) is not null, "Rating 0 rejected");
        check(ReviewsApiController.Validate(new("Nora", 6, "خدمة ممتازة وتوصيل سريع"), out _, out _) is not null, "Rating 6 rejected");
        check(ReviewsApiController.Validate(new("N", 5, "خدمة ممتازة وتوصيل سريع"), out _, out _) is not null, "Short review name rejected");
        check(ReviewsApiController.Validate(new("Nora", 5, "قصير"), out _, out _) is not null, "Short review message rejected");
        check(ReviewsApiController.Validate(new("Nora", 5, new string('a', 501)), out _, out _) is not null, "Long review message rejected");
        string? Contact(string? phone, string? email, string? message = "رسالة طويلة كفاية للإرسال") =>
            ContactApiController.Validate(new("Nora", phone, email, message), out _, out _, out _, out _);
        check(Contact("+218910000000", null) is null, "Contact with phone only accepted");
        check(Contact(null, "a@example.com") is null, "Contact with email only accepted");
        check(Contact(null, null) is not null, "Contact without phone or email rejected");
        check(Contact("abc", null) is not null, "Contact with non-digit phone rejected");
        check(Contact(null, "not-an-email") is not null, "Contact with invalid email rejected");
        check(Contact("0910000000", null, "قصير") is not null, "Short contact message rejected");

        // Receipt review validation (no HTTP).
        check(CheckoutController.ValidateReceiptReview(new(5, null, true), out _) is null, "Receipt review without a comment accepted");
        check(CheckoutController.ValidateReceiptReview(new(1, new string('a', 500), false), out _) is null, "Receipt review with 500 characters accepted");
        check(CheckoutController.ValidateReceiptReview(new(0, null, false), out _) is not null, "Receipt review rating 0 rejected");
        check(CheckoutController.ValidateReceiptReview(new(6, null, false), out _) is not null, "Receipt review rating 6 rejected");
        check(CheckoutController.ValidateReceiptReview(new(5, new string('a', 501), false), out _) is not null, "Receipt review over 500 characters rejected");
        check(CheckoutController.ValidateReceiptReview(null, out _) is not null, "Missing receipt review body rejected");
        check(ReviewsApiController.FirstWord("  Nora Ali ") == "Nora" && ReviewsApiController.FirstWord("") is null &&
            ReviewsApiController.FirstWord(null) is null, "Public review name is the first word only");

        // HTTP behaviour. Each factory has its own rate limiter state (public-forms allows 3 per hour).
        using (var factory = Create())
        using (var client = Client(factory))
        {
            // The blocked database makes the action throw; reaching it proves the route exists.
            var routeExists = true;
            try
            {
                using var anonymousGet = await client.GetAsync("/api/v1/reviews");
                routeExists = anonymousGet.StatusCode != HttpStatusCode.NotFound && anonymousGet.StatusCode != HttpStatusCode.MethodNotAllowed;
            }
            catch (InvalidOperationException) { }
            check(routeExists, "Public reviews GET route exists");
            foreach (var path in new[] { "/api/v1/reviews", "/api/v1/contact" })
            {
                using var response = await client.PostAsJsonAsync(path, new { name = "Nora" });
                check(response.StatusCode == HttpStatusCode.BadRequest, "Public POST without antiforgery rejected: " + path);
            }
            using var request = await Post(client, "/api/v1/contact", new { name = "Nora", message = "رسالة طويلة كفاية للإرسال" });
            using var invalid = await client.SendAsync(request);
            check(invalid.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(await Message(invalid)),
                "Contact without phone or email returns 400 with message");
        }
        using (var factory = Create())
        using (var client = Client(factory))
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using var request = await Post(client, "/api/v1/reviews", new { name = "Nora", rating = 9, message = "خدمة ممتازة وتوصيل سريع" });
                using var response = await client.SendAsync(request);
                check(response.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(await Message(response)),
                    "Invalid review rating returns 400 with message");
            }
            using var limited = await Post(client, "/api/v1/reviews", new { name = "Nora", rating = 9, message = "خدمة ممتازة وتوصيل سريع" });
            using var limitedResponse = await client.SendAsync(limited);
            check(limitedResponse.StatusCode == HttpStatusCode.TooManyRequests && !string.IsNullOrWhiteSpace(await Message(limitedResponse)),
                "Fourth public form post from one IP returns 429 with message");
        }

        // Idempotency-Key: a malformed key is rejected before the action (and before any database access).
        using (var factory = Create())
        using (var client = Client(factory))
        {
            using var request = await Post(client, "/api/v1/cart/items", new { variantId = 1, quantity = 1 });
            request.Headers.Add("Idempotency-Key", "bad key!");
            using var response = await client.SendAsync(request);
            check(response.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(await Message(response)),
                "Malformed Idempotency-Key returns 400 with message");
        }

        // Catalogue search/sort validation happens before any database access (a DB touch would throw).
        using (var factory = Create())
        using (var client = Client(factory))
        {
            foreach (var query in new[] { "sort=bogus", "search=" + new string('a', 101) })
            {
                using var response = await client.GetAsync("/api/v1/catalogue/products?" + query);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                check(response.StatusCode == HttpStatusCode.BadRequest && json.RootElement.TryGetProperty("title", out var title) &&
                    !string.IsNullOrWhiteSpace(title.GetString()), "Catalogue rejects invalid input with 400 and title before the database: " + query[..Math.Min(query.Length, 11)]);
            }
        }

        // Sign-in budget: 15 POSTs per 5 minutes per IP, counted before antiforgery so floods are cheap to reject.
        using (var factory = Create())
        using (var client = Client(factory))
        {
            var limited = HttpStatusCode.OK;
            string? limitMessage = null;
            for (var attempt = 1; attempt <= 16; attempt++)
            {
                using var response = await client.PostAsJsonAsync("/api/v1/account/login", new { email = "a@example.com", password = "x" });
                if (attempt <= 15 && response.StatusCode == HttpStatusCode.TooManyRequests) limited = HttpStatusCode.Conflict;
                if (attempt == 16) { limited = limited == HttpStatusCode.Conflict ? limited : response.StatusCode; limitMessage = await Message(response); }
            }
            check(limited == HttpStatusCode.TooManyRequests && !string.IsNullOrWhiteSpace(limitMessage),
                "Sixteenth sign-in POST from one IP within five minutes returns 429 with message");
        }

        // Receipt review: antiforgery, validation, then the session receipt (none here, so 404 before any database access).
        using (var factory = Create())
        using (var client = Client(factory))
        {
            using var noToken = await client.PostAsJsonAsync("/api/v1/checkout/receipt/review", new { rating = 5, allowName = false });
            check(noToken.StatusCode == HttpStatusCode.BadRequest, "Receipt review without antiforgery rejected");
            using var badRequest = await Post(client, "/api/v1/checkout/receipt/review", new { rating = 9, allowName = false });
            using var badResponse = await client.SendAsync(badRequest);
            check(badResponse.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(await Message(badResponse)),
                "Receipt review with invalid rating returns 400 with message");
            using var noReceiptRequest = await Post(client, "/api/v1/checkout/receipt/review", new { rating = 5, message = "تجربة جميلة", allowName = true });
            using var noReceipt = await client.SendAsync(noReceiptRequest);
            check(noReceipt.StatusCode == HttpStatusCode.NotFound, "Receipt review without a receipt session returns 404");
            using var limitedRequest = await Post(client, "/api/v1/checkout/receipt/review", new { rating = 5, allowName = false });
            using var limited = await client.SendAsync(limitedRequest);
            check(limited.StatusCode == HttpStatusCode.TooManyRequests && !string.IsNullOrWhiteSpace(await Message(limited)),
                "Receipt review shares the public-forms rate limit");
        }

        // Admin routes: anonymous and Customer blocked, Admin still needs antiforgery.
        using (var factory = Create())
        using (var client = Client(factory))
        {
            var reads = new[] { "/api/v1/admin/reviews", "/api/v1/admin/contact-messages" };
            var writes = new[] { "/api/v1/admin/reviews/1/visibility", "/api/v1/admin/reviews/1/delete", "/api/v1/admin/contact-messages/1/delete",
                "/api/v1/admin/contact-messages/1/read", "/api/v1/admin/products/1/archive",
                "/api/v1/admin/products/1/variants/1/archive", "/api/v1/admin/categories/1/archive" };
            foreach (var path in reads)
            {
                using var response = await client.GetAsync(path);
                check(response.StatusCode == HttpStatusCode.Unauthorized, "Anonymous admin read blocked: " + path);
            }
            foreach (var path in writes)
            {
                using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
                check(response.StatusCode == HttpStatusCode.Unauthorized, "Anonymous admin write blocked: " + path);
            }
            client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
            foreach (var path in reads)
            {
                using var response = await client.GetAsync(path);
                check(response.StatusCode == HttpStatusCode.Forbidden, "Customer admin read blocked: " + path);
            }
            foreach (var path in writes)
            {
                using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
                check(response.StatusCode == HttpStatusCode.Forbidden, "Customer admin write blocked: " + path);
            }
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            foreach (var path in writes)
            {
                using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
                check(response.StatusCode == HttpStatusCode.BadRequest, "Admin write still requires antiforgery: " + path);
            }
            foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
            {
                using var response = await client.SendAsync(new HttpRequestMessage(method, "/api/v1/admin/reviews/1"));
                check(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "No " + method + " on admin reviews");
            }
        }
    }

    private sealed class RejectDatabaseConnections : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result) => throw new InvalidOperationException("Feedback API tests must not access a database.");
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Feedback API tests must not access a database.");
    }

    private sealed class TestRoleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (role is not ("Admin" or "Customer")) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "synthetic-feedback-test"),
                new Claim(ClaimTypes.Name, "Synthetic feedback test"), new Claim(ClaimTypes.Role, role) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
