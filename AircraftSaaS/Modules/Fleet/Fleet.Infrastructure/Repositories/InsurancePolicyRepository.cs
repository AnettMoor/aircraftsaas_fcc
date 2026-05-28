using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Repositories;

internal sealed class InsurancePolicyRepository : BaseRepository<InsurancePolicy, InsurancePolicy, FleetDbContext>, IInsurancePolicyRepository
{
    public InsurancePolicyRepository(FleetDbContext dbContext)
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

    public async Task<InsurancePolicy?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
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
