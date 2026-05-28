using Fleet.Domain.Entities;
using Shared.Kernel.DAL;

namespace Fleet.Application.Contracts;

public interface IMaintenanceRecordRepository : IBaseRepository<MaintenanceRecord>
{
    Task<IEnumerable<MaintenanceRecord>> GetAllForCompanyAsync(Guid companyId, Guid? aircraftId = null);
    Task<MaintenanceRecord?> GetByIdForCompanyAsync(Guid id, Guid companyId);
    Task<MaintenanceRecord?> GetByIdForCompanyTrackingAsync(Guid id, Guid companyId);
    Task<IEnumerable<MaintenanceRecord>> GetScheduledForAircraftInRangeAsync(Guid aircraftId, DateTime start, DateTime end);
    
    /// <summary>
    /// Returns active (non-completed, non-deleted) maintenance records for an aircraft
    /// that have start and end dates set. Used by the availability service to synthesize
    /// calendar blocks for maintenance periods.
    /// </summary>
    Task<IEnumerable<MaintenanceRecord>> GetActiveForAircraftAsync(Guid aircraftId);
    
    /// <summary>
    /// Returns a soft-deleted maintenance record for the given company (tracking enabled for restore).
    /// </summary>
    Task<MaintenanceRecord?> GetDeletedByIdForCompanyTrackingAsync(Guid id, Guid companyId);
}
