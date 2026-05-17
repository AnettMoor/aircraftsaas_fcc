using App.Domain;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IAppUserCompanyRepository : IBaseRepository<AppUserCompany>
{
    Task<AppUserCompany?> GetByIdTrackingAsync(Guid id);
    
    // System-admin methods
    Task<IEnumerable<AppUserCompany>> GetAllForUserWithCompanyAsync(Guid userId);
    Task<IEnumerable<AppUserCompany>> GetAllForUserTrackingAsync(Guid userId);
    Task<(string? Name, string? Email)> GetCompanyOwnerInfoAsync(Guid companyId);
    Task<IEnumerable<string>> GetCompanyNamesForUserAsync(Guid userId);
    void RemoveRange(IEnumerable<AppUserCompany> entities);
}
