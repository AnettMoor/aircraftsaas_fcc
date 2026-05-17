using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IInsurancePolicyService
{
    Task<InsurancePolicyDto?> GetByIdAsync(Guid id, Guid aircraftId);
    Task<IEnumerable<InsurancePolicyDto>> GetAllForAircraftAsync(Guid aircraftId);
    Task<InsurancePolicyDto> CreateAsync(CreateInsurancePolicyDto dto, Guid aircraftId, Guid companyId);
    Task<InsurancePolicyDto> UpdateAsync(Guid id, UpdateInsurancePolicyDto dto, Guid aircraftId, Guid companyId);
    Task DeleteAsync(Guid id, Guid aircraftId, Guid companyId, string deletedBy);
}