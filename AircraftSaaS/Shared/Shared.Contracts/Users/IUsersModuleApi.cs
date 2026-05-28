using Shared.Contracts.Common;
using Shared.Contracts.Users.DTOs;

namespace Shared.Contracts.Users;

public interface IUsersModuleApi
{
    Task<UserBasicDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<Dictionary<Guid, UserBasicDto>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task<bool> CheckUserLicenseAsync(Guid userId, string requiredLicenseType, DateTime asOfDate, CancellationToken ct = default);
    Task<CompanyBasicDto?> GetCompanyByIdAsync(Guid companyId, CancellationToken ct = default);
    Task<Dictionary<Guid, CompanyBasicDto>> GetCompaniesByIdsAsync(IEnumerable<Guid> companyIds, CancellationToken ct = default);
    Task<List<UserBasicDto>> GetCompanyUsersAsync(Guid companyId, CancellationToken ct = default);
    Task<List<CompanySelectItemDto>> GetActiveCompaniesAsync(CancellationToken ct = default);
}
