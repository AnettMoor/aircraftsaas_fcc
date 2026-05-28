using Shared.Contracts.Common;
using Shared.Contracts.Users.DTOs;
using Shared.Kernel.DAL;
using Users.Domain.Entities;
using Users.Domain.Enums;

namespace Users.Application.Contracts;

public interface ICompanyRepository : IBaseRepository<Company>
{
    Task<Company?> GetBySlugAsync(string slug);
    Task<IEnumerable<Company>> GetAllActiveAsync();
    Task<bool> ExistsBySlugAsync(string slug);
    Task<Company?> GetByIdTrackingAsync(Guid id);
    Task<Company?> GetByIdIgnoreFiltersTrackingAsync(Guid id);
    Task<int> GetUserCountAsync(Guid companyId);
    
    // Tenant/membership methods
    Task<bool> IsCompanyOwnerAsync(Guid userId, Guid companyId);
    Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId);
    Task<EAppUserRoleInCompany?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId);
    Task<IEnumerable<AppUserCompany>> GetUserCompaniesAsync(Guid userId);
    
    // Cross-module API support methods
    Task<CompanyBasicDto?> GetBasicByIdAsync(Guid companyId, CancellationToken ct = default);
    Task<Dictionary<Guid, CompanyBasicDto>> GetBasicsByIdsAsync(IEnumerable<Guid> companyIds, CancellationToken ct = default);
    Task<List<CompanySelectItemDto>> GetActiveSelectItemsAsync(CancellationToken ct = default);
    Task<UserBasicDto?> GetUserBasicByIdAsync(Guid userId, CancellationToken ct = default);
    Task<Dictionary<Guid, UserBasicDto>> GetUserBasicsByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task<List<UserBasicDto>> GetCompanyUserBasicsAsync(Guid companyId, CancellationToken ct = default);
}
