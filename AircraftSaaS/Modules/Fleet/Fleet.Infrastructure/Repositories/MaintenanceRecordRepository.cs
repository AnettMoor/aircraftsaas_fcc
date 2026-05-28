using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Repositories;

internal sealed class MaintenanceRecordRepository : BaseRepository<MaintenanceRecord, MaintenanceRecord, FleetDbContext>, IMaintenanceRecordRepository
{
    public MaintenanceRecordRepository(FleetDbContext dbContext)
        : base(dbContext, new BaseMapper<MaintenanceRecord>())
    {
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetAllForCompanyAsync(Guid companyId, Guid? aircraftId = null)
    {
        var query = RepositoryDbSet
            .Include(m => m.Aircraft)
            .Where(m => m.Aircraft != null && m.Aircraft.CompanyId == companyId);

        if (aircraftId.HasValue)
            query = query.Where(m => m.AircraftId == aircraftId.Value);

        return await query
            .OrderByDescending(m => m.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<MaintenanceRecord?> GetByIdForCompanyAsync(Guid id, Guid companyId)
    {
        return await RepositoryDbSet
            .Include(m => m.Aircraft)
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.Aircraft != null &&
                m.Aircraft.CompanyId == companyId);
    }

    public async Task<MaintenanceRecord?> GetByIdForCompanyTrackingAsync(Guid id, Guid companyId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Include(m => m.Aircraft)
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.Aircraft != null &&
                m.Aircraft.CompanyId == companyId);
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetScheduledForAircraftInRangeAsync(
        Guid aircraftId, DateTime start, DateTime end)
    {
        var records = await RepositoryDbSet
            .Where(m => m.AircraftId == aircraftId &&
                        ((start >= m.StartDate && start < m.EndDate) ||
                         (end > m.StartDate && end <= m.EndDate) ||
                         (start <= m.StartDate && end >= m.EndDate)))
            .ToListAsync();

        // Filter by LangStr Status in memory
        return records.Where(m => m.Status.ToString() == "Scheduled");
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetActiveForAircraftAsync(Guid aircraftId)
    {
        var records = await RepositoryDbSet
            .Where(m => m.AircraftId == aircraftId &&
                        !m.IsCompleted)
            .OrderBy(m => m.StartDate ?? m.MaintenanceDate)
            .ToListAsync();

        // Filter by LangStr Status in memory (Scheduled or InProgress)
        return records.Where(m =>
            m.Status.ToString() == "Scheduled" ||
            m.Status.ToString() == "InProgress");
    }

    public async Task<MaintenanceRecord?> GetDeletedByIdForCompanyTrackingAsync(Guid id, Guid companyId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .IgnoreQueryFilters()
            .Include(m => m.Aircraft)
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.Aircraft != null &&
                m.Aircraft.CompanyId == companyId &&
                m.DeletedAt != null);
    }
}
