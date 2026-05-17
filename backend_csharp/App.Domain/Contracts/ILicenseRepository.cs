using App.Domain.Entities;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface ILicenseRepository : IBaseRepository<License>
{
    Task<IEnumerable<License>> GetAllForUserAsync(Guid userId);
    Task<License?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<License?> GetByIdTrackingAsync(Guid id);
    Task<IEnumerable<License>> GetValidLicensesForUserAsync(Guid userId);
    Task<bool> HasValidLicenseForTypeAsync(Guid userId, string licenseType, DateTime asOfDate);
}
