using Shared.Contracts.Common;
using Users.Application.Interfaces;

namespace WebApp.Middleware;

/// <summary>
/// Middleware to resolve tenant from URL path (e.g., /acme/aircraft -> company "acme")
/// and set the X-Tenant-Id header for downstream services.
/// Also auto-selects first company for authenticated users.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    
    // Path prefixes that should be excluded from tenant resolution
    private static readonly HashSet<string> ExcludedPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/",
        "/swagger",
        "/health",
        "/lib/",
        "/css/",
        "/js/",
        "/images/",
        "/favicon.ico",
        "/companyowner/",
        "/bookings/",
        "/aircraft/",
        "/airports/",
        "/companies/",
        "/maintenance/",
        "/reviews/",
        "/user/",
        "/admin/",
        "/home/",
        "/identity/"
    };
    
    // Exact paths to exclude
    private static readonly HashSet<string> ExcludedExactPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/home",
        "/companyowner",
        "/bookings",
        "/aircraft",
        "/airports",
        "/companies",
        "/maintenance",
        "/reviews",
        "/user",
        "/admin",
        "/identity",
        "/account/login",
        "/account/logout",
        "/account/register"
    };

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ICompanyService companyService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        // Always validate/auto-select company cookie for authenticated users (runs on ALL paths)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            if (!context.Request.Cookies.ContainsKey("SelectedCompanyId"))
            {
                await AutoSelectFirstCompanyAsync(context, tenantContext);
            }
            else
            {
                // Validate that the cookie's company still exists AND the user is still a member
                var cookieValue = context.Request.Cookies["SelectedCompanyId"];
                if (Guid.TryParse(cookieValue, out var cookieCompanyId))
                {
                    var companyActive = await companyService.IsCompanyActiveAsync(cookieCompanyId);
                    
                    var userStillMember = false;
                    if (companyActive)
                    {
                        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var cookieUserId))
                        {
                            userStillMember = await tenantContext.IsUserInCompanyAsync(cookieCompanyId, cookieUserId);
                        }
                    }

                    if (!companyActive || !userStillMember)
                    {
                        _logger.LogWarning(
                            "Stale SelectedCompanyId cookie '{CompanyId}' — company {Reason}. Re-selecting.",
                            cookieCompanyId,
                            !companyActive ? "no longer exists or is inactive" : "user is no longer a member");
                        context.Response.Cookies.Delete("SelectedCompanyId");
                        await AutoSelectFirstCompanyAsync(context, tenantContext);
                    }
                }
            }
        }
        
        // Skip slug-based tenant resolution for excluded paths (MVC controllers, static assets, etc.)
        if (ShouldSkipResolution(path))
        {
            await _next(context);
            return;
        }
        
        // Skip if tenant header already set (from authenticated request)
        if (context.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            await _next(context);
            return;
        }

        // Try to extract tenant slug from URL path
        var slug = ExtractSlugFromPath(path);
        
        if (!string.IsNullOrEmpty(slug))
        {
            _logger.LogDebug("Extracted tenant slug '{Slug}' from path '{Path}'", slug, path);
            
            // Resolve slug to company ID using shared contract
            var tenantId = await tenantContext.ResolveTenantIdFromSlugAsync(slug);
            
            if (tenantId.HasValue)
            {
                context.Items["TenantId"] = tenantId.Value;
                context.Request.Headers["X-Tenant-Id"] = tenantId.Value.ToString();
                _logger.LogDebug("Resolved tenant slug '{Slug}' to TenantId '{TenantId}'", slug, tenantId);
            }
            else
            {
                _logger.LogWarning("Could not resolve tenant slug '{Slug}' - company may not exist or be inactive", slug);
            }
        }

        await _next(context);
    }

    private async Task AutoSelectFirstCompanyAsync(
        HttpContext context, 
        ITenantContext tenantContext)
    {
        try
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return;

            // Get user's company IDs via shared contract
            var companyIds = await tenantContext.GetUserCompanyIdsAsync(userId);
            var firstCompanyId = companyIds.FirstOrDefault();

            if (firstCompanyId == Guid.Empty)
                return;

            // Auto-select first company
            context.Response.Cookies.Append("SelectedCompanyId", firstCompanyId.ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(30),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

            // Also make the tenant available for the CURRENT request (cookie is only available on next request)
            context.Items["TenantId"] = firstCompanyId;
            context.Request.Headers["X-Tenant-Id"] = firstCompanyId.ToString();

            _logger.LogDebug("Auto-selected company '{CompanyId}' for user '{UserId}'",
                firstCompanyId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error auto-selecting first company: {Error}", ex.Message);
        }
    }

    private static bool ShouldSkipResolution(string path)
    {
        // Check exact matches first
        if (ExcludedExactPaths.Contains(path))
            return true;
        
        // Check if path starts with any excluded prefix
        foreach (var excludedPrefix in ExcludedPathPrefixes)
        {
            if (path.StartsWith(excludedPrefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        return false;
    }

    private static string? ExtractSlugFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Skip if path starts with /api (API paths are handled differently)
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return null;

        // Remove leading slash and get first segment
        var segments = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (segments.Length > 0)
        {
            var firstSegment = segments[0];
            
            // Validate slug format (lowercase letters, numbers, hyphens)
            if (IsValidSlugFormat(firstSegment))
            {
                return firstSegment;
            }
        }

        return null;
    }

    private static bool IsValidSlugFormat(string slug)
    {
        // Slug should only contain lowercase letters, numbers, and hyphens
        if (string.IsNullOrEmpty(slug) || slug.Length < 2 || slug.Length > 50)
            return false;
        
        if (slug.StartsWith('-') || slug.EndsWith('-'))
            return false;
        
        return slug.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-');
    }
}

/// <summary>
/// Extension methods for adding TenantResolutionMiddleware to the pipeline.
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>
    /// Adds the TenantResolutionMiddleware to the application pipeline.
    /// This middleware extracts the company slug from URL path and resolves it to a TenantId.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }
}
