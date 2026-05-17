using App.Domain.Entities;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IInsurancePolicyRepository : IBaseRepository<InsurancePolicy>
{
    Task<IEnumerable<InsurancePolicy>> GetAllForAircraftAsync(Guid aircraftId);
    Task<InsurancePolicy?> GetByIdForAircraftAsync(Guid id, Guid aircraftId);
    
    /// <summary>
    /// IDOR-safe tracking variant: fetches a policy only if it belongs to the specified aircraft.
    /// </summary>
    Task<InsurancePolicy?> GetByIdForAircraftTrackingAsync(Guid id, Guid aircraftId);
    
    Task<InsurancePolicy?> GetActiveForAircraftAsync(Guid aircraftId);
    Task<bool> HasActivePolicyAsync(Guid aircraftId);
}
