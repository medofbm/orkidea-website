using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Shipping quotes: one budget per client IP (a single script must not block every customer's quote).
    options.AddPolicy("shipping-preview", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Order placement and cart writes: per-IP budgets against fake-order floods. Only writes count, so browsing and
    // reading the cart never use the budget. CI and test hosts that place many orders from one IP raise them by config.
    var checkoutLimit = builder.Configuration.GetValue("RateLimits:CheckoutPer10Minutes", 10);
    var cartWriteLimit = builder.Configuration.GetValue("RateLimits:CartWritesPerMinute", 120);
    options.AddPolicy("checkout-place", context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = checkoutLimit, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("read"));
    options.AddPolicy("cart-write", context => HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = cartWriteLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("read"));
    // Public forms (contact, store review, receipt review): spam protection per client IP, with a separate budget
    // per form so one form (or a few typos) never blocks another. 5 posts per form per 15 minutes.
    options.AddPolicy("public-forms", context => RateLimitPartition.GetFixedWindowLimiter(
        (context.Connection.RemoteIpAddress?.ToString() ?? "unknown") + "|" + context.Request.Path.Value?.ToLowerInvariant(),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = PublicFormLimit, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    // Password recovery gets its own, looser budget so a few typos do not lock a customer out for an hour.
    options.AddPolicy("account-recovery", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    // Sign-in (password and verification code) and sign-up get their own per-IP budgets against password spraying and
    // mass sign-ups. Only POSTs count, so opening or refreshing the login page never uses the budget.
    var signInLimit = builder.Configuration.GetValue("RateLimits:SignInPerFiveMinutes", 15);
    var signUpLimit = builder.Configuration.GetValue("RateLimits:SignUpPerHour", 5);
    options.AddPolicy("account-sign-in", context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = signInLimit, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("read"));
    options.AddPolicy("account-sign-up", context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = signUpLimit, Window = TimeSpan.FromHours(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("read"));
    var previousRejection = options.OnRejected;
    options.OnRejected = async (context, token) =>
    {
        var path = context.HttpContext.Request.Path;
        if (path.StartsWithSegments("/api/v1/account/login") || path.StartsWithSegments("/api/v1/account/verify") ||
            path.StartsWithSegments("/api/v1/account/register"))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { message = "محاولات كثيرة من نفس الجهاز. جربي بعد شوية." }, token);
        }
        else if (path.StartsWithSegments("/api/v1/account/forgot-password") || path.StartsWithSegments("/api/v1/account/reset-password"))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { message = "جربتي أكثر من مرة، جربي بعد ربع ساعة." }, token);
        }
        else if (path.StartsWithSegments("/api/v1/reviews") || path.StartsWithSegments("/api/v1/contact") ||
            path.StartsWithSegments("/api/v1/checkout/receipt/review"))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { message = "وصلنا عدد كبير من الرسائل منك، جربي مرة ثانية بعد ساعة." }, token);
        }
        else if (path.StartsWithSegments("/api/v1/checkout/orders") || path.StartsWithSegments("/Checkout/PlaceOrder", StringComparison.OrdinalIgnoreCase))
        {
            await WriteBusyAsync(context.HttpContext, "محاولات كثيرة لإرسال الطلب من نفس الجهاز. جربي بعد شوية.", token);
        }
        else if (path.StartsWithSegments("/api/v1/checkout/shipping") || path.StartsWithSegments("/Checkout/PreviewShipping", StringComparison.OrdinalIgnoreCase))
        {
            await WriteBusyAsync(context.HttpContext, "طلبات كثيرة لحساب الشحن. جربي بعد دقيقة.", token);
        }
        else if (path.StartsWithSegments("/api/v1/cart") || path.StartsWithSegments("/Cart", StringComparison.OrdinalIgnoreCase))
        {
            await WriteBusyAsync(context.HttpContext, "عمليات كثيرة على السلة. جربي بعد دقيقة.", token);
        }
        else if (previousRejection is not null) await previousRejection(context, token);
    };
});
static async Task WriteBusyAsync(HttpContext http, string message, CancellationToken token)
{
    http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    if (http.Request.Path.StartsWithSegments("/api")) await http.Response.WriteAsJsonAsync(new { message }, token);
    else
    {
        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.WriteAsync(message, token);
    }
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    foreach (var value in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (!IPAddress.TryParse(value, out var proxy)) throw new InvalidOperationException("Invalid trusted proxy address.");
        options.KnownProxies.Add(proxy);
    }
});
var protection = builder.Services.AddDataProtection().SetApplicationName(
    builder.Configuration["DataProtection:ApplicationName"] ?? "Orkidea");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// ============================================
// SERVICE CONFIGURATION
// ============================================

// Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            // Handle transient Azure SQL failures (e.g. database not available temporarily)
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Identity Configuration with Security Options
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;

    // Sign-in settings
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Password reset email: SMTP sender replaces the default no-op IEmailSender; reset links live 2 hours.
builder.Services.Configure<Orkideya.Services.Email.EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<Orkideya.Services.Email.AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, Orkideya.Services.Email.SmtpEmailSender>();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

// Application Services
Orkideya.Infrastructure.Shipping.ShippingRegistration.AddCarrierReadGateway(builder.Services, builder.Configuration);
builder.Services.AddScoped<Orkideya.Infrastructure.Shipping.SabilRateImport>();
builder.Services.AddSingleton<ExcelExportService>();
builder.Services.AddSingleton<ProductImageResizer>();
builder.Services.AddSingleton<ProductImageStorage>();
builder.Services.AddHostedService<ProductImageBackfillService>();
builder.Services.AddScoped<Orkideya.Application.Products.ProductEditor>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Orkideya.Application.Orders.OrderService>();
builder.Services.AddScoped<Orkideya.Application.Orders.ManualOrderService>();
builder.Services.AddScoped<Orkideya.Application.Orders.OrderStatusService>();
builder.Services.AddScoped<Orkideya.Application.Inventory.StockService>();
builder.Services.AddScoped<Orkideya.Application.Delivery.DeliveryService>();
builder.Services.AddScoped<Orkideya.Application.Accounts.IAccountActivityRecorder, Orkideya.Application.Accounts.AccountActivityRecorder>();

// Determine cookie security policy based on environment
var cookieSecurePolicy = builder.Environment.IsDevelopment() 
    ? CookieSecurePolicy.SameAsRequest 
    : CookieSecurePolicy.Always;

builder.Services.ConfigureApplicationCookie(options => options.Cookie.SecurePolicy = cookieSecurePolicy);
builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
    IdentityConstants.TwoFactorUserIdScheme, options => options.Cookie.SecurePolicy = cookieSecurePolicy);

// Session Configuration with Security
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache(); // Idempotency-Key replay store (see Infrastructure/Http/IdempotentAttribute).
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// Anti-forgery Configuration
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// HSTS Configuration
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();
// Fail fast outside Development: a missing connection string or key ring must stop startup with a clear message,
// not surface later as a 500 on the first request or as cookies and reset tokens that die on every restart.
if (!app.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection")))
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is required outside Development. Set it (for example ConnectionStrings__DefaultConnection).");
    // The one-shot migrate/initialize run does not issue cookies or tokens, so it does not need the key ring.
    if (!app.Configuration.GetValue<bool>("Initialization:Run") && string.IsNullOrWhiteSpace(app.Configuration["DataProtection:KeysPath"]))
        throw new InvalidOperationException(
            "DataProtection:KeysPath is required outside Development so cookies and reset tokens survive restarts. Set it (for example DataProtection__KeysPath).");
}
app.UseForwardedHeaders();
if (app.Environment.IsStaging())
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        await next();
    });

// ============================================
// DATABASE INITIALIZATION
// ============================================
// Run migrations/bootstrap only as an explicit maintenance command.
if (builder.Configuration.GetValue<bool>("Initialization:Run"))
{
    using var scope = app.Services.CreateScope();
    await DbInitializer.Initialize(scope.ServiceProvider);
    return;
}

// ============================================
// MIDDLEWARE PIPELINE CONFIGURATION
// ============================================

// Environment-specific Configuration
if (!app.Environment.IsDevelopment())
{
    // A 500 under /api returns JSON ProblemDetails; every other path keeps the HTML error page.
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch => branch.UseExceptionHandler(errors => errors.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "حدث خطأ غير متوقع" };
        problem.Extensions["message"] = problem.Title;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    })));
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch => branch.UseExceptionHandler("/Home/Error"));
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    // X-Content-Type-Options: Prevent MIME type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    
    // X-Frame-Options: Prevent clickjacking
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    
    // X-XSS-Protection: Enable XSS protection
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    
    // Referrer-Policy: Control referrer information
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Permissions-Policy: Control browser features
    context.Response.Headers.Append("Permissions-Policy", 
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
    
    // Content-Security-Policy: Enhanced XSS protection
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com data:; " +
        "img-src 'self' data: https: blob:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self';");
    
    await next();
});

app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());
// Never serve customer spreadsheets from the public document root.
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles();

app.UseSession();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
  name: "areas",
  pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapGet("/health/live", () => Results.Text("live"));
app.MapGet("/health/ready", async (ApplicationDbContext context, CancellationToken cancellationToken) =>
{
    try
    {
        var ready = await context.Database.CanConnectAsync(cancellationToken) &&
            !(await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        return Results.Text(ready ? "ready" : "not ready", statusCode: ready ? 200 : 503);
    }
    catch { return Results.Text("not ready", statusCode: 503); }
});

app.Run();
public partial class Program
{
    /// <summary>Posts allowed per public form, per client IP, per 15 minutes.</summary>
    public const int PublicFormLimit = 5;
}
