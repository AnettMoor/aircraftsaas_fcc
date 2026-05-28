using Fleet.Application.DTOs;

namespace Fleet.Application.Interfaces;

public interface IAirportService
{
    Task<IEnumerable<AirportDto>> GetAllAirportsAsync();
    Task<AirportDto?> GetAirportByIdAsync(Guid id);
    Task<AirportDto?> GetAirportByIcaoCodeAsync(string icaoCode);
    Task<IEnumerable<AirportDto>> SearchAirportsAsync(string? searchTerm);
    Task<AirportDto> CreateAirportAsync(CreateAirportDto dto, string createdBy);
    Task<AirportDto> UpdateAirportAsync(Guid id, UpdateAirportDto dto, string updatedBy);
    Task DeleteAirportAsync(Guid id, string deletedBy);
    Task<bool> RestoreAirportAsync(Guid id);
}
