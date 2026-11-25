using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace Orkideya.Infrastructure.Http;

/// <summary>
/// Makes a create-style POST safe to repeat. The client sends an <c>Idempotency-Key</c> header (one value per
/// user intent, e.g. per form until it succeeds). The first request runs; a repeat with the same key returns the
/// first successful result without running the action again, and a repeat while the first is still running gets 409.
/// Requests without the header run normally, so older clients keep working. Keys are scoped to the route and the
/// caller (signed-in user, otherwise client IP), and kept for 10 minutes in memory (single-instance deployment).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed partial class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public const string Header = "Idempotency-Key";
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, byte> Running = new();

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex KeyFormat();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue(Header, out var values))
        {
            await next();
            return;
        }

        var key = values.ToString();
        if (values.Count != 1 || !KeyFormat().IsMatch(key))
        {
            context.Result = new BadRequestObjectResult(new { message = "مفتاح الطلب غير صالح." });
            return;
        }

        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        var caller = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var scope = string.Join('|', "idem", http.Request.Method, http.Request.Path.Value?.ToLowerInvariant(), caller, key);

        if (cache.TryGetValue(scope, out StoredResult? stored) && stored is not null)
        {
            http.Response.Headers["Idempotent-Replay"] = "true";
            context.Result = stored.ToResult();
            return;
        }
        if (!Running.TryAdd(scope, 0))
        {
            context.Result = new ConflictObjectResult(new { message = "الطلب قيد المعالجة، استني شوية." });
            return;
        }

        try
        {
            // The first request may have finished between the cache check above and taking the lock.
            if (cache.TryGetValue(scope, out StoredResult? finished) && finished is not null)
            {
                http.Response.Headers["Idempotent-Replay"] = "true";
                context.Result = finished.ToResult();
                return;
            }
            var executed = await next();
            // Only successful results are remembered; a failure can be retried with the same key.
            var result = StoredResult.From(executed.Result);
            if (executed.Exception is null && result is { Status: >= 200 and < 300 } && !ReportsFailure(result.Value))
                cache.Set(scope, result, Lifetime);
        }
        finally
        {
            Running.TryRemove(scope, out _);
        }
    }

    // Some older JSON actions (the cart) answer 200 with { success: false } on failure; those are not remembered.
    internal static bool ReportsFailure(object? value)
    {
        if (value is null) return false;
        var property = value.GetType().GetProperty("success") ?? value.GetType().GetProperty("Success");
        return property?.GetValue(value) is false;
    }

    internal sealed record StoredResult(int Status, object? Value, bool Json)
    {
        public static StoredResult? From(IActionResult? result) => result switch
        {
            ObjectResult o => new(o.StatusCode ?? StatusCodes.Status200OK, o.Value, false),
            JsonResult j => new(j.StatusCode ?? StatusCodes.Status200OK, j.Value, true),
            StatusCodeResult s => new(s.StatusCode, null, false),
            _ => null
        };

        public IActionResult ToResult() => Json
            ? new JsonResult(Value) { StatusCode = Status }
            : Value is null ? new StatusCodeResult(Status) : new ObjectResult(Value) { StatusCode = Status };
    }
}
