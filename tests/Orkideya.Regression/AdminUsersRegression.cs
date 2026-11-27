using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkideya.Application.Accounts;
using Orkideya.Controllers;
using Orkideya.Services.Email;

/// <summary>
/// Admin accounts: only the super admin adds or removes admins, the super admin is never removed, nobody removes
/// themselves, adding an admin never takes a password, and store sign-ins are recorded. In-memory users, no database,
/// no SMTP. All addresses are synthetic (example.test).
/// </summary>
internal static class AdminUsersRegression
{
    private const string OwnerEmail = "owner@example.test";
    private const string HelperEmail = "helper@example.test";
    private const string CustomerEmail = "customer@example.test";
    private const string ShopperEmail = "shopper@example.test";

    private sealed class CapturingSender : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = new();
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Sent.Add((email, subject));
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingActivity : IAccountActivityRecorder
    {
        public List<string> Created { get; } = new();
        public List<string> SignIns { get; } = new();
        public Task RecordCreatedAsync(string userId, bool signedIn, CancellationToken cancellationToken = default)
        {
            Created.Add(userId);
            return Task.CompletedTask;
        }
        public Task RecordSignInAsync(string userId, CancellationToken cancellationToken = default)
        {
            SignIns.Add(userId);
            return Task.CompletedTask;
        }
    }

    /// <summary>In-memory users with roles and passwords.</summary>
    internal sealed class MemoryUserStore : IUserEmailStore<IdentityUser>, IUserSecurityStampStore<IdentityUser>,
        IUserRoleStore<IdentityUser>, IUserPasswordStore<IdentityUser>
    {
        public Dictionary<string, IdentityUser> Users { get; } = new();
        public Dictionary<string, HashSet<string>> Roles { get; } = new();

        public void Add(IdentityUser user, params string[] roles)
        {
            user.NormalizedEmail = user.Email!.ToUpperInvariant();
            user.NormalizedUserName = user.UserName!.ToUpperInvariant();
            user.SecurityStamp ??= Guid.NewGuid().ToString("N");
            Users[user.Id] = user;
            Roles[user.Id] = roles.Select(role => role.ToUpperInvariant()).ToHashSet();
        }

        public void Dispose() { }
        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken ct) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(IdentityUser user, string? name, CancellationToken ct) { user.NormalizedUserName = name; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken ct)
        {
            Users[user.Id] = user;
            Roles[user.Id] = new HashSet<string>();
            return Task.FromResult(IdentityResult.Success);
        }
        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken ct) { Users.Remove(user.Id); return Task.FromResult(IdentityResult.Success); }
        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult(Users.GetValueOrDefault(userId));
        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
            Task.FromResult(Users.Values.FirstOrDefault(user => user.NormalizedUserName == normalizedUserName));
        public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken ct) { user.Email = email; return Task.CompletedTask; }
        public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.Email);
        public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.EmailConfirmed);
        public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken ct) { user.EmailConfirmed = confirmed; return Task.CompletedTask; }
        public Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
            Task.FromResult(Users.Values.FirstOrDefault(user => user.NormalizedEmail == normalizedEmail));
        public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.NormalizedEmail);
        public Task SetNormalizedEmailAsync(IdentityUser user, string? email, CancellationToken ct) { user.NormalizedEmail = email; return Task.CompletedTask; }
        public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken ct) { user.SecurityStamp = stamp; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);
        public Task AddToRoleAsync(IdentityUser user, string roleName, CancellationToken ct) { Roles[user.Id].Add(roleName); return Task.CompletedTask; }
        public Task RemoveFromRoleAsync(IdentityUser user, string roleName, CancellationToken ct) { Roles[user.Id].Remove(roleName); return Task.CompletedTask; }
        public Task<IList<string>> GetRolesAsync(IdentityUser user, CancellationToken ct) =>
            Task.FromResult<IList<string>>(Roles[user.Id].Select(role => role == "ADMIN" ? "Admin" : role).ToList());
        public Task<bool> IsInRoleAsync(IdentityUser user, string roleName, CancellationToken ct) => Task.FromResult(Roles[user.Id].Contains(roleName));
        public Task<IList<IdentityUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct) =>
            Task.FromResult<IList<IdentityUser>>(Users.Values.Where(user => Roles[user.Id].Contains(roleName)).ToList());
        public Task SetPasswordHashAsync(IdentityUser user, string? passwordHash, CancellationToken ct) { user.PasswordHash = passwordHash; return Task.CompletedTask; }
        public Task<string?> GetPasswordHashAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash);
        public Task<bool> HasPasswordAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash is not null);
    }

    /// <summary>Test-only scheme: header X-Test-User names the signed-in admin (Admin role claim, user id claim).</summary>
    private sealed class TestAdminHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var id = Request.Headers["X-Test-User"].ToString();
            if (id.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, id), new Claim(ClaimTypes.Role, "Admin") }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    public static async Task Run(Action<bool, string> check)
    {
        // Rules.
        check(AdminAccountRules.IsSuperAdminEmail(" Owner@Example.test ", OwnerEmail), "Super admin email matches case-insensitively and trimmed");
        check(!AdminAccountRules.IsSuperAdminEmail(OwnerEmail, "") && !AdminAccountRules.IsSuperAdminEmail(OwnerEmail, null),
            "No configured super admin email means nobody is super admin");
        check(!AdminAccountRules.IsSuperAdminEmail(null, OwnerEmail) && !AdminAccountRules.IsSuperAdminEmail(HelperEmail, OwnerEmail),
            "Other emails are not the super admin");
        check(AdminAccountRules.RemovalRefusal("a", "a", HelperEmail, true, OwnerEmail) == AdminAccountRules.SelfRemovalMessage,
            "Rule: an admin cannot remove themselves");
        check(AdminAccountRules.RemovalRefusal("a", "b", "OWNER@example.test", true, OwnerEmail) == AdminAccountRules.SuperAdminProtectedMessage,
            "Rule: the super admin can never be removed");
        check(AdminAccountRules.RemovalRefusal("a", "b", CustomerEmail, false, OwnerEmail) == AdminAccountRules.NotAnAdminMessage,
            "Rule: removing a non-admin is refused");
        check(AdminAccountRules.RemovalRefusal("a", "b", HelperEmail, true, OwnerEmail) is null, "Rule: the super admin may remove another admin");

        // Shape: no password anywhere in the add-admin input; writes need antiforgery; Admin role at class level.
        check(!typeof(AddAdminInput).GetProperties().Any(p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)),
            "Add-admin input has no password field");
        var controller = typeof(AdminUsersController);
        check(controller.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Roles == "Admin"), "Admin users controller requires the Admin role");
        foreach (var name in new[] { nameof(AdminUsersController.AddAdmin), nameof(AdminUsersController.RemoveAdmin) })
            check(controller.GetMethod(name)!.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>().Any(), "Admin write requires antiforgery: " + name);

        // HTTP with in-memory users.
        var store = new MemoryUserStore();
        var hasher = new PasswordHasher<IdentityUser>();
        var owner = new IdentityUser { Id = "owner-id", UserName = OwnerEmail, Email = OwnerEmail, EmailConfirmed = true };
        var helper = new IdentityUser { Id = "helper-id", UserName = HelperEmail, Email = HelperEmail, EmailConfirmed = true };
        var customer = new IdentityUser { Id = "customer-id", UserName = CustomerEmail, Email = CustomerEmail, EmailConfirmed = false };
        customer.PasswordHash = hasher.HashPassword(customer, "Synthetic_Aa1!x");
        var shopper = new IdentityUser { Id = "shopper-id", UserName = ShopperEmail, Email = ShopperEmail, EmailConfirmed = false };
        const string shopperPassword = "Synthetic_Bb2!y";
        shopper.PasswordHash = hasher.HashPassword(shopper, shopperPassword);
        store.Add(owner, "Admin");
        store.Add(helper, "Admin");
        store.Add(customer);
        store.Add(shopper);
        var sender = new CapturingSender();
        var activity = new CapturingActivity();

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
                services.RemoveAll<IAccountActivityRecorder>();
                services.AddSingleton<IAccountActivityRecorder>(activity);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "AdminUsersTest";
                    options.DefaultChallengeScheme = "AdminUsersTest";
                    options.DefaultForbidScheme = "AdminUsersTest";
                }).AddScheme<AuthenticationSchemeOptions, TestAdminHandler>("AdminUsersTest", _ => { });
            });
        });

        async Task<HttpClient> ClientFor(string? userId)
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
            });
            if (userId is not null) client.DefaultRequestHeaders.Add("X-Test-User", userId);
            using var session = await client.GetAsync("/api/v1/session");
            using var json = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Add("RequestVerificationToken", json.RootElement.GetProperty("requestToken").GetString());
            return client;
        }
        static async Task<string?> Message(HttpResponseMessage response)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }

        using (var helperClient = await ClientFor(helper.Id))
        {
            using var add = await helperClient.PostAsJsonAsync("/api/v1/admin/admins", new { email = "someone@example.test" });
            check(add.StatusCode == HttpStatusCode.Forbidden && await Message(add) == AdminAccountRules.NotSuperAdminMessage &&
                store.Users.Values.All(user => user.Email != "someone@example.test"), "A normal admin cannot add administrators (403)");
            using var remove = await helperClient.PostAsync("/api/v1/admin/admins/" + owner.Id + "/remove", null);
            check(remove.StatusCode == HttpStatusCode.Forbidden && store.Roles[owner.Id].Contains("ADMIN"),
                "A normal admin cannot remove administrators, the super admin stays (403)");
        }

        using (var ownerClient = await ClientFor(owner.Id))
        {
            using var noToken = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = new Uri("https://localhost") };
            noToken.DefaultRequestHeaders.Add("X-Test-User", owner.Id);
            using var unsafeAdd = await noToken.PostAsJsonAsync("/api/v1/admin/admins", new { email = "someone@example.test" });
            check(unsafeAdd.StatusCode == HttpStatusCode.BadRequest, "Adding an admin without antiforgery is rejected");

            using var self = await ownerClient.PostAsync("/api/v1/admin/admins/" + owner.Id + "/remove", null);
            check(self.StatusCode == HttpStatusCode.BadRequest && await Message(self) == AdminAccountRules.SelfRemovalMessage &&
                store.Roles[owner.Id].Contains("ADMIN"), "The super admin cannot remove themselves");

            using var created = await ownerClient.PostAsJsonAsync("/api/v1/admin/admins",
                new { email = "brother@example.test", password = "Ignored_Aa1!" });
            var brother = store.Users.Values.SingleOrDefault(user => user.Email == "brother@example.test");
            check(created.StatusCode == HttpStatusCode.OK && brother is { EmailConfirmed: true, PasswordHash: null } &&
                store.Roles[brother.Id].Contains("ADMIN"), "Super admin adds a new admin by email; the account has no password");
            check(!(await created.Content.ReadAsStringAsync()).Contains("Ignored_Aa1!", StringComparison.Ordinal),
                "A password sent with the request is ignored and never returned");
            check(sender.Sent.Count == 1 && sender.Sent[0].To == "brother@example.test" && sender.Sent[0].Subject == PasswordResetEmail.Subject,
                "The new admin receives the password-reset email to choose a password");
            check(activity.Created.SequenceEqual(new[] { brother!.Id }), "The new admin account gets a creation date");

            using var again = await ownerClient.PostAsJsonAsync("/api/v1/admin/admins", new { email = "Brother@Example.test" });
            check(again.StatusCode == HttpStatusCode.Conflict && sender.Sent.Count == 1, "Adding an existing admin again is refused");

            var customerStamp = customer.SecurityStamp;
            using var promoted = await ownerClient.PostAsJsonAsync("/api/v1/admin/admins", new { email = CustomerEmail });
            check(promoted.StatusCode == HttpStatusCode.OK && store.Roles[customer.Id].Contains("ADMIN") &&
                customer is { EmailConfirmed: true, PasswordHash: null } && customer.SecurityStamp != customerStamp &&
                sender.Sent.Count == 2 && sender.Sent[1].To == CustomerEmail,
                "An unconfirmed existing account loses its old password and sessions before it becomes admin, then gets the email");

            var helperStamp = helper.SecurityStamp;
            using var removed = await ownerClient.PostAsync("/api/v1/admin/admins/" + helper.Id + "/remove", null);
            check(removed.StatusCode == HttpStatusCode.OK && !store.Roles[helper.Id].Contains("ADMIN") &&
                store.Users.ContainsKey(helper.Id) && helper.SecurityStamp != helperStamp,
                "Super admin removes another admin; the account stays and its sessions are reset");
            using var removedAgain = await ownerClient.PostAsync("/api/v1/admin/admins/" + helper.Id + "/remove", null);
            check(removedAgain.StatusCode == HttpStatusCode.BadRequest && await Message(removedAgain) == AdminAccountRules.NotAnAdminMessage,
                "Removing an account that is no longer admin is refused");
            using var missing = await ownerClient.PostAsync("/api/v1/admin/admins/no-such-user/remove", null);
            check(missing.StatusCode == HttpStatusCode.NotFound, "Removing an unknown account returns 404");
        }

        using (var guest = await ClientFor(null))
        {
            using var anonymous = await guest.PostAsJsonAsync("/api/v1/admin/admins", new { email = "someone@example.test" });
            check(anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "Anonymous callers cannot add administrators");

            using var wrong = await guest.PostAsJsonAsync("/api/v1/account/login", new { email = ShopperEmail, password = "Wrong_Aa1!", rememberMe = false });
            check(wrong.StatusCode == HttpStatusCode.Unauthorized && activity.SignIns.Count == 0, "A failed sign-in records no activity");
            using var login = await guest.PostAsJsonAsync("/api/v1/account/login", new { email = ShopperEmail, password = shopperPassword, rememberMe = false });
            check(login.StatusCode == HttpStatusCode.OK && activity.SignIns.SequenceEqual(new[] { shopper.Id }),
                "A successful password sign-in records the sign-in for that account");
        }
    }
}
