using Fleet.Domain.Entities;
using Shared.Kernel.DAL;

namespace Fleet.Application.Contracts;

public interface IAirportRepository : IBaseRepository<Airport>
{
    Task<Airport?> GetByIcaoCodeAsync(string icaoCode);
    Task<IEnumerable<Airport>> SearchAsync(string? searchTerm);
    Task<Airport?> GetByIdTrackingAsync(Guid id);
    Task<Airport?> GetByIdIgnoreFiltersTrackingAsync(Guid id);
    Task<int> CountAllAsync(CancellationToken ct = default);
}
