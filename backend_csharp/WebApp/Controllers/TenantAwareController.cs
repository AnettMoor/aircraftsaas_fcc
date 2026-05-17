using System.Security.Claims;
using App.Application.Interfaces;
using App.Domain;
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
    protected readonly ITenantService TenantService;
    protected readonly ICompanyService CompanyService;

    private bool _companyDeactivated;

    protected TenantAwareController(ITenantService tenantService, ICompanyService companyService)
    {
        TenantService = tenantService;
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
        return await TenantService.IsUserInCompanyAsync(tenantId.Value, userId.Value);
    }

    /// <summary>
    /// Check whether the current user holds the <see cref="EAppUserRoleInCompany.Normal"/>
    /// (pilot) role in the given tenant.
    /// </summary>
    protected async Task<bool> IsUserNormalRoleAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue) return false;
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return false;
        var role = await TenantService.GetUserRoleInCompanyAsync(tenantId.Value, userId.Value);
        return role == EAppUserRoleInCompany.Normal;
    }

    /// <summary>
    /// Check whether the current user is a CompanyOwner for the given tenant.
    /// </summary>
    protected async Task<bool> IsCompanyOwnerAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue) return false;
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return false;
        return await TenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value);
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

        var tenantId = TenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                var userCompanies = await TenantService.GetUserCompaniesAsync(userId.Value);
                var first = userCompanies.FirstOrDefault();
                if (first != null)
                {
                    TenantService.SetCurrentTenant(first.CompanyId);
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
            TempData["Error"] = "You are not authorized to access this company.";
            return null;
        }

        if (!await IsCompanyActiveAsync(tenantId.Value))
        {
            _companyDeactivated = true;
            return null;
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
