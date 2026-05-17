using App.Domain.Entities;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IAirportRepository : IBaseRepository<Airport>
{
    Task<Airport?> GetByIcaoCodeAsync(string icaoCode);
    Task<IEnumerable<Airport>> SearchAsync(string? searchTerm);
    Task<Airport?> GetByIdTrackingAsync(Guid id);
    Task<Airport?> GetByIdIgnoreFiltersTrackingAsync(Guid id);
    
    // System-admin methods
    Task<IEnumerable<Airport>> GetAllIgnoreFiltersAsync();
    Task<int> CountAllActiveAsync();
    Task<bool> ExistsByIcaoCodeIgnoreFiltersAsync(string icaoCode, Guid? excludeId = null);
}
