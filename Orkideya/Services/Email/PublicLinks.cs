using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Orkideya.Services.Email;

/// <summary>
/// Builds absolute links for emails from the configured public base URL (App:PublicBaseUrl), never from the
/// request Host header, so a forged Host cannot poison reset or confirmation links.
/// </summary>
public static class PublicLinks
{
    /// <summary>
    /// Joins the configured base URL and a relative path. Without a usable base URL the request origin is used
    /// only in Development; otherwise returns null (the caller must not send the email).
    /// </summary>
    public static string? Combine(string? publicBaseUrl, string? relativePath, bool isDevelopment, string requestScheme, string requestHost)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        // Only a path may be appended: refuse anything that could carry its own host.
        if (!relativePath.StartsWith('/') || relativePath.StartsWith("//") || relativePath.Contains((char)92)) return null; // backslash
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            if (!Uri.TryCreate(publicBaseUrl.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https")) return null;
            return publicBaseUrl.Trim().TrimEnd('/') + relativePath;
        }
        return isDevelopment && !string.IsNullOrWhiteSpace(requestHost)
            ? requestScheme + "://" + requestHost + relativePath
            : null;
    }

    /// <summary>
    /// Turns the relative path from <c>Url.Page(...)</c> (called without a protocol) into a public absolute link.
    /// Returns null and logs a warning when no public base URL is configured outside Development.
    /// </summary>
    public static string? PublicPageLink(this PageModel page, string? relativePath)
    {
        var services = page.HttpContext.RequestServices;
        var options = services.GetRequiredService<IOptions<AppOptions>>().Value;
        var environment = services.GetRequiredService<IHostEnvironment>();
        var link = Combine(options.PublicBaseUrl, relativePath, environment.IsDevelopment(),
            page.Request.Scheme, page.Request.Host.Value ?? "");
        if (link is null)
            services.GetRequiredService<ILoggerFactory>().CreateLogger("Orkideya.Services.Email.PublicLinks")
                .LogWarning("App:PublicBaseUrl is empty or invalid; account email not sent.");
        return link;
    }
}
