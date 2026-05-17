using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain.Entities;
using Base.Domain;

namespace App.Application.Services;

public class AirportService : IAirportService
{
    private readonly IAppUOW _uow;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AirportService(IAppUOW uow, ICurrentUserProvider currentUserProvider)
    {
        _uow = uow;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IEnumerable<AirportDto>> GetAllAirportsAsync()
    {
        var airports = await _uow.AirportRepository.AllAsync();
        return airports.OrderBy(a => a.Name.ToString()).Select(MapToDto);
    }

    public async Task<AirportDto?> GetAirportByIdAsync(Guid id)
    {
        var airport = await _uow.AirportRepository.FindAsync(id);
        return airport == null ? null : MapToDto(airport);
    }

    public async Task<AirportDto?> GetAirportByIcaoCodeAsync(string icaoCode)
    {
        var airport = await _uow.AirportRepository.GetByIcaoCodeAsync(icaoCode);
        return airport == null ? null : MapToDto(airport);
    }

    public async Task<IEnumerable<AirportDto>> SearchAirportsAsync(string? searchTerm)
    {
        var airports = await _uow.AirportRepository.SearchAsync(searchTerm);
        return airports.Select(MapToDto);
    }

    public async Task<AirportDto> CreateAirportAsync(CreateAirportDto dto)
    {
        var createdBy = _currentUserProvider.GetCurrentUserSubject()
                        ?? _currentUserProvider.GetCurrentUserName()
                        ?? "system";

        var airport = new Airport
        {
            IcaoCode = dto.IcaoCode.ToUpper(),
            IataCode = dto.IataCode.ToUpper(),
            Name = new LangStr(dto.Name),
            City = new LangStr(dto.City),
            Country = new LangStr(dto.Country),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Elevation = dto.Elevation,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _uow.AirportRepository.Add(airport);
        await _uow.SaveChangesAsync();

        return MapToDto(airport);
    }

    public async Task<AirportDto> UpdateAirportAsync(Guid id, UpdateAirportDto dto)
    {
        var airport = await _uow.AirportRepository.GetByIdTrackingAsync(id);
        if (airport == null)
        {
            throw new InvalidOperationException("Airport not found");
        }

        var updatedBy = _currentUserProvider.GetCurrentUserSubject()
                        ?? _currentUserProvider.GetCurrentUserName()
                        ?? "system";

        airport.IcaoCode = dto.IcaoCode.ToUpper();
        airport.IataCode = dto.IataCode.ToUpper();
        airport.Name.SetTranslation(dto.Name);
        airport.City.SetTranslation(dto.City);
        airport.Country.SetTranslation(dto.Country);
        airport.Latitude = dto.Latitude;
        airport.Longitude = dto.Longitude;
        airport.Elevation = dto.Elevation;
        airport.UpdatedBy = updatedBy;
        airport.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return MapToDto(airport);
    }

    public async Task DeleteAirportAsync(Guid id)
    {
        var airport = await _uow.AirportRepository.GetByIdTrackingAsync(id);
        if (airport == null)
        {
            throw new InvalidOperationException("Airport not found");
        }

        var userId = _currentUserProvider.GetCurrentUserSubject() ?? "system";
        airport.SoftDelete(userId);

        await _uow.SaveChangesAsync();
    }

    public async Task<bool> RestoreAirportAsync(Guid id)
    {
        var airport = await _uow.AirportRepository.GetByIdIgnoreFiltersTrackingAsync(id);

        if (airport == null || !airport.IsDeleted)
        {
            return false;
        }

        airport.Restore();
        await _uow.SaveChangesAsync();

        return true;
    }

    private static AirportDto MapToDto(Airport airport)
    {
        return new AirportDto
        {
            Id = airport.Id,
            IcaoCode = airport.IcaoCode,
            IataCode = airport.IataCode,
            Name = airport.Name.ToString(),
            City = airport.City.ToString(),
            Country = airport.Country.ToString(),
            Latitude = airport.Latitude,
            Longitude = airport.Longitude,
            Elevation = airport.Elevation
        };
    }
}
