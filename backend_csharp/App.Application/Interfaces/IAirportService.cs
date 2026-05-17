using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IAirportService
{
    Task<IEnumerable<AirportDto>> GetAllAirportsAsync();
    Task<AirportDto?> GetAirportByIdAsync(Guid id);
    Task<AirportDto?> GetAirportByIcaoCodeAsync(string icaoCode);
    Task<IEnumerable<AirportDto>> SearchAirportsAsync(string? searchTerm);
    Task<AirportDto> CreateAirportAsync(CreateAirportDto dto);
    Task<AirportDto> UpdateAirportAsync(Guid id, UpdateAirportDto dto);
    Task DeleteAirportAsync(Guid id);
    Task<bool> RestoreAirportAsync(Guid id);
}
