using Shared.Contracts.Fleet.DTOs;

namespace Shared.Contracts.Fleet;

public interface IFleetModuleApi
{
    Task<AircraftBasicDto?> GetAircraftByIdAsync(Guid aircraftId, CancellationToken ct = default);
    Task<Dictionary<Guid, AircraftBasicDto>> GetAircraftsByIdsAsync(IEnumerable<Guid> aircraftIds, CancellationToken ct = default);
    Task<bool> CheckAircraftAvailabilityAsync(Guid aircraftId, DateTime startDateTime, DateTime endDateTime, CancellationToken ct = default);
    Task<int> GetAircraftCountByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<List<AircraftBasicDto>> GetAircraftsByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<int> GetTotalAircraftCountAsync(CancellationToken ct = default);
    Task<int> GetTotalAirportsCountAsync(CancellationToken ct = default);
    Task<Guid> BlockAircraftAvailabilityAsync(Guid aircraftId, Guid? bookingId, DateTime startDateTime, DateTime endDateTime, string availabilityType, string? reason, CancellationToken ct = default);
}
