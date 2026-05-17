using App.Domain;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface ICompanyRepository : IBaseRepository<Company>
{
    Task<Company?> GetBySlugAsync(string slug);
    Task<IEnumerable<Company>> GetAllActiveAsync();
    Task<bool> ExistsBySlugAsync(string slug);
    Task<Company?> GetByIdTrackingAsync(Guid id);
    Task<Company?> GetByIdIgnoreFiltersTrackingAsync(Guid id);
    Task<int> GetUserCountAsync(Guid companyId);
    Task<int> GetAircraftCountAsync(Guid companyId);
    
    // Tenant/membership methods
    Task<bool> IsCompanyOwnerAsync(Guid userId, Guid companyId);
    Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId);
    Task<EAppUserRoleInCompany?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId);
    Task<IEnumerable<AppUserCompany>> GetUserCompaniesAsync(Guid userId);
    
    // System-admin methods
    Task<IEnumerable<Company>> GetAllNonDeletedAsync();
    Task<Company?> GetByIdNonDeletedTrackingAsync(Guid id);
    Task<bool> ExistsBySlugIgnoreFiltersAsync(string slug);
    Task<bool> ExistsByIdNonDeletedAsync(Guid id);
}
