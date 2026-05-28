using Fleet.Domain.Entities;
using Shared.Kernel.DAL;

namespace Fleet.Application.Contracts;

public interface IInsurancePolicyRepository : IBaseRepository<InsurancePolicy>
{
    Task<IEnumerable<InsurancePolicy>> GetAllForAircraftAsync(Guid aircraftId);
    Task<InsurancePolicy?> GetByIdForAircraftAsync(Guid id, Guid aircraftId);
    Task<InsurancePolicy?> GetByIdTrackingAsync(Guid id);
    Task<InsurancePolicy?> GetActiveForAircraftAsync(Guid aircraftId);
    Task<bool> HasActivePolicyAsync(Guid aircraftId);
}
