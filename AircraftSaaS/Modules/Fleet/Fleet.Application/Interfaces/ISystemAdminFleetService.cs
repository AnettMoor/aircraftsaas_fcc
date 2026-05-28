using Fleet.Application.DTOs;

namespace Fleet.Application.Interfaces;

public interface ISystemAdminFleetService
{
    // ── All Aircraft (system-wide) ───────────────────────────────────────────
    Task<AircraftListDto> GetAllAircraftAsync(string? search, Guid? tenantId, bool? available, int page, int pageSize);

    // ── Airports ─────────────────────────────────────────────────────────────
    Task<AirportsListDto> GetAirportsAsync(string? search, bool showDeleted, int page, int pageSize);
    Task<AirportEditDto?> GetAirportForEditAsync(Guid id);
    Task<bool> AirportExistsByIcaoCodeAsync(string icaoCode, Guid? excludeId = null);
    Task<bool> HasActiveAircraftAtAirportAsync(Guid airportId);
}
