using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Repositories;

internal sealed class AirportRepository : BaseRepository<Airport, Airport, FleetDbContext>, IAirportRepository
{
    public AirportRepository(FleetDbContext dbContext)
        : base(dbContext, new BaseMapper<Airport>())
    {
    }

    public async Task<Airport?> GetByIcaoCodeAsync(string icaoCode)
    {
        return await RepositoryDbSet.FirstOrDefaultAsync(a => a.IcaoCode == icaoCode);
    }

    public async Task<IEnumerable<Airport>> SearchAsync(string? searchTerm)
    {
        var allAirports = await RepositoryDbSet.ToListAsync();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            return allAirports
                .Where(a =>
                    a.Name.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.IcaoCode.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.IataCode.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.City.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.Country.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name.ToString());
        }

        return allAirports.OrderBy(a => a.Name.ToString());
    }

    public async Task<Airport?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Airport?> GetByIdIgnoreFiltersTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
    }

    // ── Cross-module API support ──

    public async Task<int> CountAllAsync(CancellationToken ct = default)
    {
        return await RepositoryDbSet.CountAsync(ct);
    }
}
