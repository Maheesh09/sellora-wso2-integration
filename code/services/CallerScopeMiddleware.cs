// Source: https://github.com/Team-Sellora/sellora-order/blob/500d36a848d5e2dc2c87a1b1b67eea7ca217d3fa/src/Sellora.OrderService.Api/Identity/CallerScopeMiddleware.cs
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sellora.OrderService.Application.Dependencies;
using Sellora.OrderService.Application.Identity;

namespace Sellora.OrderService.Api.Identity;

public sealed class CallerScopeOptions
{
    public const string Section = "CallerScope";

    /// <summary>How long a caller's scope is reused. A reassignment in Organization shows up within this window.</summary>
    public int CacheSeconds { get; set; } = 300;

    /// <summary>Shorter cache for "no profile", so a newly created user works quickly.</summary>
    public int MissingProfileCacheSeconds { get; set; } = 30;
}

/// <summary>
/// Resolves the caller's hierarchy position once per request from
/// Organization's GET /api/me/scope (cached per user), so business IDs never
/// have to be copied into WSO2 IS claims. Company Admins are unrestricted
/// within their company and skip the lookup, so they keep working even when
/// Organization is down.
/// </summary>
public sealed class CallerScopeMiddleware(RequestDelegate next)
{
    public const string ItemKey = "sellora.caller-scope";

    public async Task InvokeAsync(
        HttpContext context,
        IOrganizationClient organization,
        IMemoryCache cache,
        IOptions<CallerScopeOptions> options,
        ILogger<CallerScopeMiddleware> logger)
    {
        var user = context.User;
        var role = SelloraRoles.ByBreadth.FirstOrDefault(candidate => user.HasClaim("roles", candidate));

        if (user.Identity?.IsAuthenticated != true || role is null || role == SelloraRoles.CompanyAdmin)
        {
            await next(context);
            return;
        }

        var key = $"caller-scope:{user.FindFirst("companyId")?.Value}:{user.FindFirst("sub")?.Value}:{role}";

        if (!cache.TryGetValue(key, out CallerScope? scope))
        {
            try
            {
                var resolved = await organization.GetCallerScopeAsync(context.RequestAborted);
                scope = resolved ?? CallerScope.Empty;

                cache.Set(key, scope, TimeSpan.FromSeconds(resolved is null
                    ? options.Value.MissingProfileCacheSeconds
                    : options.Value.CacheSeconds));

                if (resolved is null)
                {
                    logger.LogWarning(
                        "No Organization profile for {Role} {Subject}; the caller sees nothing",
                        role, user.FindFirst("sub")?.Value);
                }
            }
            catch (DependencyUnavailableException exception)
            {
                logger.LogWarning(exception, "Could not resolve the caller's scope from Organization");

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.RetryAfter = "30";
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Dependency unavailable",
                    Detail = "Organization service is currently unavailable, so your access could not be resolved. Try again shortly.",
                    Extensions = { ["dependency"] = nameof(Dependency.Organization) }
                });
                return;
            }
        }

        context.Items[ItemKey] = scope;
        await next(context);
    }
}
