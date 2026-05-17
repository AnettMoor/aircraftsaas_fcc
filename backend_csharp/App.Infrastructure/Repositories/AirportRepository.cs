using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class AirportRepository : BaseRepository<Airport, Airport, AppDbContext>, IAirportRepository
{
    public AirportRepository(AppDbContext dbContext, IBaseMapper<Airport, Airport> mapper)
        : base(dbContext, mapper)
    {
    }

    public AirportRepository(AppDbContext dbContext)
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

    // System-admin methods
    
    public async Task<IEnumerable<Airport>> GetAllIgnoreFiltersAsync()
    {
        return await RepositoryDbSet
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    public async Task<int> CountAllActiveAsync()
    {
        return await RepositoryDbSet.CountAsync(a => a.DeletedAt == null);
    }

    public async Task<bool> ExistsByIcaoCodeIgnoreFiltersAsync(string icaoCode, Guid? excludeId = null)
    {
        var query = RepositoryDbSet.IgnoreQueryFilters()
            .Where(a => a.IcaoCode == icaoCode.ToUpper());

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}
