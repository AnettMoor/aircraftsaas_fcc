using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class InsurancePolicyRepository : BaseRepository<InsurancePolicy, InsurancePolicy, AppDbContext>, IInsurancePolicyRepository
{
    public InsurancePolicyRepository(AppDbContext dbContext, IBaseMapper<InsurancePolicy, InsurancePolicy> mapper)
        : base(dbContext, mapper)
    {
    }

    public InsurancePolicyRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<InsurancePolicy>())
    {
    }

    public async Task<IEnumerable<InsurancePolicy>> GetAllForAircraftAsync(Guid aircraftId)
    {
        return await RepositoryDbSet
            .Where(p => p.AircraftId == aircraftId)
            .OrderByDescending(p => p.EndDate)
            .ToListAsync();
    }

    public async Task<InsurancePolicy?> GetByIdForAircraftAsync(Guid id, Guid aircraftId)
    {
        return await RepositoryDbSet
            .FirstOrDefaultAsync(p => p.Id == id && p.AircraftId == aircraftId);
    }

    /// <summary>
    /// IDOR-safe tracking variant: fetches a policy only if it belongs to the specified aircraft.
    /// </summary>
    public async Task<InsurancePolicy?> GetByIdForAircraftTrackingAsync(Guid id, Guid aircraftId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.AircraftId == aircraftId);
    }

    public async Task<InsurancePolicy?> GetActiveForAircraftAsync(Guid aircraftId)
    {
        var now = DateTime.UtcNow;
        return await RepositoryDbSet
            .Where(p => p.AircraftId == aircraftId && p.StartDate <= now && p.EndDate >= now)
            .OrderByDescending(p => p.EndDate)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasActivePolicyAsync(Guid aircraftId)
    {
        var now = DateTime.UtcNow;
        return await RepositoryDbSet
            .AnyAsync(p => p.AircraftId == aircraftId && p.StartDate <= now && p.EndDate >= now);
    }
}