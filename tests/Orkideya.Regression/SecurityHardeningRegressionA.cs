using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Orkideya.Controllers;

/// <summary>
/// No-database guards for the hardening batch. F-1: there is no default-deny fallback policy yet, so every admin
/// controller must carry [Authorize(Roles = "Admin")] at class level and no admin action may be [AllowAnonymous].
/// Adding an unprotected admin controller makes this test fail.
/// </summary>
internal static class SecurityHardeningRegressionA
{
    public static void Run(Action<bool, string> check)
    {
        var controllers = AdminControllers(typeof(CartController).Assembly).ToList();
        check(controllers.Count >= 10, "Admin controller discovery finds the admin surface (" + controllers.Count + " controllers)");
        foreach (var type in controllers)
        {
            check(HasAdminRole(type), "Admin controller requires the Admin role at class level: " + type.FullName);
            var anonymousActions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()).Select(method => method.Name).ToList();
            check(!type.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() && anonymousActions.Count == 0,
                "Admin controller has no [AllowAnonymous] on class or actions: " + type.FullName);
        }
        // Self-check of the rule itself, so the guard cannot silently pass on everything.
        check(!HasAdminRole(typeof(AdminSampleUnprotected)), "Guard rejects an admin controller without the Admin role");
        check(IsAdminController(typeof(AdminSampleUnprotected)), "Guard recognises a controller named Admin* as an admin controller");
        check(!HasAdminRole(typeof(UserOnlyAdminRouteSample)) && IsAdminController(typeof(UserOnlyAdminRouteSample)),
            "Guard rejects an api/v1/admin route that only requires a signed-in user");
    }

    private static IEnumerable<Type> AdminControllers(Assembly assembly) => assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type) && IsAdminController(type));

    private static bool IsAdminController(Type type)
    {
        if (type.Name.StartsWith("Admin", StringComparison.Ordinal)) return true;
        if (type.Namespace?.Contains(".Areas.Admin", StringComparison.Ordinal) == true) return true;
        var templates = type.GetCustomAttributes<RouteAttribute>(true).Select(route => route.Template)
            .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(method => method.GetCustomAttributes().OfType<IRouteTemplateProvider>().Select(route => route.Template)));
        return templates.Any(template => template is not null &&
            template.TrimStart('/').StartsWith("api/v1/admin", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAdminRole(Type type) => type.GetCustomAttributes<AuthorizeAttribute>(true).Any(attribute =>
        (attribute.Roles ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains("Admin", StringComparer.Ordinal));

    private sealed class AdminSampleUnprotected : ControllerBase { }

    [Route("api/v1/admin/sample")]
    [Authorize]
    private sealed class UserOnlyAdminRouteSample : ControllerBase { }
}
