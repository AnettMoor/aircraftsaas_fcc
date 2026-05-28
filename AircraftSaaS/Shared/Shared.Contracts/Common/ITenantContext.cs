namespace Shared.Contracts.Common;

/// <summary>
/// Minimal tenant context interface for cross-module use.
/// Fleet.Api and Booking.Api depend on this instead of the full ITenantService from Users.Application.
/// </summary>
public interface ITenantContext
{
    Guid? GetCurrentTenantId();
    void SetCurrentTenant(Guid companyId);
    Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId);
    Task<bool> IsUserCompanyOwnerAsync(Guid companyId, Guid userId);
    Task<Guid?> ResolveOrAutoSetTenantAsync(Guid userId);

    /// <summary>
    /// Returns the current authenticated user's ID, or null if not authenticated.
    /// </summary>
    Guid? GetCurrentUserId();

    /// <summary>
    /// Returns the user's role name in the given company (e.g. "CompanyOwner", "Normal"),
    /// or null if the user is not a member.  Uses a string rather than a domain enum
    /// so that consuming modules don't depend on Users.Domain.
    /// </summary>
    Task<string?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId);

    /// <summary>
    /// Returns the IDs of all companies the user belongs to.
    /// Avoids leaking the Users.Domain entity <c>AppUserCompany</c>.
    /// </summary>
    Task<IEnumerable<Guid>> GetUserCompanyIdsAsync(Guid userId);

    // ── Methods added to eliminate ITenantService leakage ───────────────

    /// <summary>
    /// Returns lightweight summaries of all companies the user belongs to,
    /// including company name, role (as string), and active status.
    /// Avoids leaking Users.Domain entities/enums to the host or other modules.
    /// </summary>
    Task<IEnumerable<UserCompanySummary>> GetUserCompanySummariesAsync(Guid userId);

    /// <summary>
    /// Resolve a URL slug (e.g. "acme") to a company ID, or null if not found.
    /// </summary>
    Task<Guid?> ResolveTenantIdFromSlugAsync(string slug);

    /// <summary>
    /// Extract the tenant slug from the current request path, if any.
    /// </summary>
    string? GetCurrentTenantSlug();
}
