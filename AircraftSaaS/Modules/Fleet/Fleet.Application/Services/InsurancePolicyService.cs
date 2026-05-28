using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Kernel.Domain;

namespace Fleet.Application.Services;

internal sealed class InsurancePolicyService : IInsurancePolicyService
{
    private readonly IFleetUOW _uow;

    public InsurancePolicyService(IFleetUOW uow)
    {
        _uow = uow;
    }

    public async Task<InsurancePolicyDto?> GetByIdAsync(Guid id, Guid aircraftId)
    {
        var policy = await _uow.InsurancePolicyRepository.GetByIdForAircraftAsync(id, aircraftId);
        return policy == null ? null : MapToDto(policy);
    }

    public async Task<IEnumerable<InsurancePolicyDto>> GetAllForAircraftAsync(Guid aircraftId)
    {
        var policies = await _uow.InsurancePolicyRepository.GetAllForAircraftAsync(aircraftId);
        return policies.Select(MapToDto);
    }

    public async Task<InsurancePolicyDto> CreateAsync(CreateInsurancePolicyDto dto, Guid aircraftId, Guid companyId)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var policy = new InsurancePolicy
        {
            AircraftId = aircraftId,
            PolicyNumber = dto.PolicyNumber,
            InsuranceProvider = new LangStr(dto.InsuranceProvider),
            StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
            CoverageAmount = dto.CoverageAmount,
            CoverageType = new LangStr(dto.CoverageType)
        };

        _uow.InsurancePolicyRepository.Add(policy);
        await _uow.SaveChangesAsync();

        return MapToDto(policy);
    }

    public async Task<InsurancePolicyDto> UpdateAsync(Guid id, UpdateInsurancePolicyDto dto, Guid aircraftId, Guid companyId)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var policy = await _uow.InsurancePolicyRepository.GetByIdTrackingAsync(id);
        if (policy == null || policy.AircraftId != aircraftId)
            throw new InvalidOperationException("Insurance policy not found.");

        policy.PolicyNumber = dto.PolicyNumber;
        policy.InsuranceProvider.SetTranslation(dto.InsuranceProvider);
        policy.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        policy.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
        policy.CoverageAmount = dto.CoverageAmount;
        policy.CoverageType.SetTranslation(dto.CoverageType);

        await _uow.SaveChangesAsync();

        return MapToDto(policy);
    }

    public async Task DeleteAsync(Guid id, Guid aircraftId, Guid companyId, string deletedBy)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var policy = await _uow.InsurancePolicyRepository.GetByIdTrackingAsync(id);
        if (policy == null || policy.AircraftId != aircraftId)
            throw new InvalidOperationException("Insurance policy not found.");

        policy.SoftDelete(deletedBy);
        await _uow.SaveChangesAsync();
    }

    private static InsurancePolicyDto MapToDto(InsurancePolicy policy) => new()
    {
        Id = policy.Id,
        AircraftId = policy.AircraftId,
        PolicyNumber = policy.PolicyNumber,
        InsuranceProvider = policy.InsuranceProvider.ToString(),
        StartDate = policy.StartDate,
        EndDate = policy.EndDate,
        CoverageAmount = policy.CoverageAmount,
        CoverageType = policy.CoverageType.ToString(),
        IsActive = policy.IsActive
    };
}
