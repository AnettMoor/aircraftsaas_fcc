using System.Net.Http.Json;
using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;

namespace WebApp.Proxies;

/// <summary>
/// HTTP proxy for ISystemAdminFleetService — delegates all fleet admin operations
/// to the Fleet microservice via REST calls to internal endpoints.
/// </summary>
public class FleetAdminServiceProxy : ISystemAdminFleetService
{
    private readonly HttpClient _http;
    private readonly ILogger<FleetAdminServiceProxy> _logger;

    public FleetAdminServiceProxy(HttpClient http, ILogger<FleetAdminServiceProxy> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<AircraftListDto> GetAllAircraftAsync(
        string? search, Guid? tenantId, bool? available, int page, int pageSize)
    {
        try
        {
            var url = $"api/v1/internal/fleet/admin/aircraft?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (tenantId.HasValue) url += $"&tenantId={tenantId}";
            if (available.HasValue) url += $"&available={available}";

            return await _http.GetFromJsonAsync<AircraftListDto>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all aircraft from Fleet service");
            return new AircraftListDto();
        }
    }

    public async Task<AirportsListDto> GetAirportsAsync(
        string? search, bool showDeleted, int page, int pageSize)
    {
        try
        {
            var url = $"api/v1/internal/fleet/admin/airports?page={page}&pageSize={pageSize}&showDeleted={showDeleted}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";

            return await _http.GetFromJsonAsync<AirportsListDto>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get airports from Fleet service");
            return new AirportsListDto();
        }
    }

    public async Task<AirportEditDto?> GetAirportForEditAsync(Guid id)
    {
        try
        {
            return await _http.GetFromJsonAsync<AirportEditDto>(
                $"api/v1/internal/fleet/admin/airports/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get airport {AirportId} from Fleet service", id);
            return null;
        }
    }

    public async Task<bool> AirportExistsByIcaoCodeAsync(string icaoCode, Guid? excludeId = null)
    {
        try
        {
            var url = $"api/v1/internal/fleet/admin/airports/exists?icaoCode={Uri.EscapeDataString(icaoCode)}";
            if (excludeId.HasValue) url += $"&excludeId={excludeId}";

            return await _http.GetFromJsonAsync<bool>(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check airport ICAO code from Fleet service");
            return false;
        }
    }

    public async Task<bool> HasActiveAircraftAtAirportAsync(Guid airportId)
    {
        try
        {
            return await _http.GetFromJsonAsync<bool>(
                $"api/v1/internal/fleet/admin/airports/{airportId}/has-aircraft");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check active aircraft at airport {AirportId}", airportId);
            return false;
        }
    }
}
