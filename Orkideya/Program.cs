using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
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

// Application Services
builder.Services.AddSingleton<ExcelExportService>();
builder.Services.AddSingleton<ProductImageStorage>();
builder.Services.AddScoped<WhatsAppNotificationService>();
builder.Services.AddScoped<Orkideya.Application.Notifications.IOrderNotificationTransport>(services => services.GetRequiredService<WhatsAppNotificationService>());
builder.Services.AddScoped<Orkideya.Application.Notifications.NotificationDispatcher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<Orkideya.Application.Notifications.NotificationWorker>();
builder.Services.AddScoped<Orkideya.Application.Orders.OrderService>();
builder.Services.AddScoped<Orkideya.Application.Orders.OrderStatusService>();

// Determine cookie security policy based on environment
var cookieSecurePolicy = builder.Environment.IsDevelopment() 
    ? CookieSecurePolicy.SameAsRequest 
    : CookieSecurePolicy.Always;

// Session Configuration with Security
builder.Services.AddDistributedMemoryCache();
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
    app.UseExceptionHandler("/Home/Error");
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
public partial class Program { }
