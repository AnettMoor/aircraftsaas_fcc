using Fleet.Application.DTOs;

namespace Fleet.Application.Interfaces;

public interface IAircraftAvailabilityService
{
    Task<AircraftAvailabilityDto?> GetByIdAsync(Guid id, Guid aircraftId);
    Task<IEnumerable<AircraftAvailabilityDto>> GetAllForAircraftAsync(Guid aircraftId);
    Task<AircraftAvailabilityDto> CreateAsync(CreateAircraftAvailabilityDto dto, Guid aircraftId, Guid companyId);
    Task<AircraftAvailabilityDto> UpdateAsync(Guid id, UpdateAircraftAvailabilityDto dto, Guid aircraftId, Guid companyId);
    Task DeleteAsync(Guid id, Guid aircraftId, Guid companyId, string deletedBy);
}
