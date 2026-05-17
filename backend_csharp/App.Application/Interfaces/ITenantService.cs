namespace App.Application.Interfaces;

using App.Domain;

public interface ITenantService
{
    Guid? GetCurrentTenantId();
    string? GetCurrentTenantSlug();
    Task<Guid?> ResolveTenantIdFromSlugAsync(string slug);
    Task<Guid?> ResolveTenantIdFromHostAsync(string host);
    void SetCurrentTenant(Guid tenantId);
    Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId);
    Task<bool> IsUserCompanyOwnerAsync(Guid companyId, Guid userId);
    Task<EAppUserRoleInCompany?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId);
    Task<IEnumerable<AppUserCompany>> GetUserCompaniesAsync(Guid userId);
    Guid? GetCurrentUserId();
}
