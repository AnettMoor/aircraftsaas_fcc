using System.Security.Claims;
using Shared.Contracts.Common;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers;

/// <summary>
/// Base controller providing shared tenant-resolution, user-identity, and
/// authorization helpers used across tenant-scoped MVC controllers.
/// <para>
/// Eliminates the following boilerplate that was previously copy-pasted
/// into every feature controller: <c>GetCurrentUserId</c>,
/// <c>IsUserAuthorizedForTenantAsync</c>, <c>IsUserNormalRoleAsync</c>,
/// <c>IsCompanyOwnerAsync</c>, <c>IsCompanyActiveAsync</c>,
/// <c>GetTenantIdOrRedirect</c>, and <c>RedirectAfterTenantCheck</c>.
/// </para>
/// </summary>
public abstract class TenantAwareController : Controller
{
    protected readonly ITenantContext TenantContext;
    protected readonly ICompanyService CompanyService;

    private bool _companyDeactivated;

    protected TenantAwareController(ITenantContext tenantContext, ICompanyService companyService)
    {
        TenantContext = tenantContext;
        CompanyService = companyService;
    }

    // ── Identity helpers ──────────────────────────────────────────────

    /// <summary>
    /// Get the current authenticated user's <see cref="Guid"/> from claims.
    /// Returns <c>null</c> when the user is not authenticated or the claim is missing.
    /// </summary>
    protected Guid? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value is string s
            && Guid.TryParse(s, out var id)
            ? id
            : null;
    }

    /// <summary>
    /// Get the current authenticated user's ID as a raw string (useful for
    /// <c>CreatedBy</c> / <c>UpdatedBy</c> audit fields).
    /// </summary>
    protected string? GetCurrentUserIdString()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    // ── Tenant & authorization helpers ────────────────────────────────

    /// <summary>
    /// Check whether the current user is a member of the specified tenant.
    /// </summary>
    protected async Task<bool> IsUserAuthorizedForTenantAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue) return false;
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return false;
        return await TenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value);
    }

    /// <summary>
    /// Check whether the current user holds the "Normal" (pilot) role
    /// in the given tenant.
    /// </summary>
    protected async Task<bool> IsUserNormalRoleAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue) return false;
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return false;
        var role = await TenantContext.GetUserRoleInCompanyAsync(tenantId.Value, userId.Value);
        return role == "Normal";
    }

    /// <summary>
    /// Check whether the current user is a CompanyOwner for the given tenant.
    /// </summary>
    protected async Task<bool> IsCompanyOwnerAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue) return false;
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return false;
        return await TenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value);
    }

    /// <summary>
    /// Check whether the specified company is active (not deactivated by a SystemAdmin).
    /// </summary>
    protected async Task<bool> IsCompanyActiveAsync(Guid tenantId)
    {
        return await CompanyService.IsCompanyActiveAsync(tenantId);
    }

    // ── Tenant resolution ─────────────────────────────────────────────

    /// <summary>
    /// Resolve the current tenant ID.  Auto-selects the user's first company
    /// when no tenant is currently set.  Returns <c>null</c> when no valid
    /// tenant can be resolved — callers should then use
    /// <see cref="RedirectAfterTenantCheck"/> to return the appropriate redirect.
    /// </summary>
    protected async Task<Guid?> GetTenantIdOrRedirect()
    {
        _companyDeactivated = false;

        var tenantId = TenantContext.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        // Resolve all user companies once so we can recover from stale/invalid tenant context
        var userCompanies = userId.HasValue
            ? (await TenantContext.GetUserCompanySummariesAsync(userId.Value)).ToList()
            : new List<UserCompanySummary>();

        if (!tenantId.HasValue)
        {
            if (userCompanies.Any())
            {
                // Prefer first active company to avoid immediate redirects to CompanyDeactivated
                var first = userCompanies.FirstOrDefault(uc => uc.IsActive)
                            ?? userCompanies.FirstOrDefault();
                if (first != null)
                {
                    TenantContext.SetCurrentTenant(first.CompanyId);
                    tenantId = first.CompanyId;
                }
            }
        }

        if (!tenantId.HasValue)
        {
            TempData["Error"] = "No active company context. Please select a company.";
            return null;
        }

        if (!await IsUserAuthorizedForTenantAsync(tenantId))
        {
            // Recover from stale tenant cookie by switching to any company the user belongs to
            var fallback = userCompanies.FirstOrDefault(uc => uc.IsActive)
                           ?? userCompanies.FirstOrDefault();
            if (fallback != null)
            {
                TenantContext.SetCurrentTenant(fallback.CompanyId);
                tenantId = fallback.CompanyId;
            }
            else
            {
                TempData["Error"] = "You are not authorized to access this company.";
                return null;
            }
        }

        if (!await IsCompanyActiveAsync(tenantId.Value))
        {
            // If selected company is inactive, try another active company before redirecting
            var activeFallback = userCompanies.FirstOrDefault(uc => uc.IsActive);
            if (activeFallback != null && activeFallback.CompanyId != tenantId.Value)
            {
                TenantContext.SetCurrentTenant(activeFallback.CompanyId);
                tenantId = activeFallback.CompanyId;
            }
            else
            {
                _companyDeactivated = true;
                return null;
            }
        }

        return tenantId;
    }

    /// <summary>
    /// Returns the appropriate redirect when <see cref="GetTenantIdOrRedirect"/>
    /// yields <c>null</c>.  Redirects to the "CompanyDeactivated" page if the
    /// company was inactive; otherwise to Home/Index.
    /// </summary>
    protected IActionResult RedirectAfterTenantCheck()
        => _companyDeactivated
            ? RedirectToAction("CompanyDeactivated", "Home")
            : RedirectToAction("Index", "Home");
}
