using System.Net.Http.Json;
using Shared.Contracts.Fleet;
using Shared.Contracts.Fleet.DTOs;

namespace Booking.WebHost.Proxies;

public class FleetServiceHttpClient : IFleetModuleApi
{
    private readonly HttpClient _http;
    private readonly ILogger<FleetServiceHttpClient> _logger;

    public FleetServiceHttpClient(HttpClient http, ILogger<FleetServiceHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<AircraftBasicDto?> GetAircraftByIdAsync(
        Guid aircraftId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<AircraftBasicDto>(
                $"api/v1/internal/fleet/aircraft/{aircraftId}", ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get aircraft {AircraftId}", aircraftId);
            return null;
        }
    }

    public async Task<Dictionary<Guid, AircraftBasicDto>> GetAircraftsByIdsAsync(
        IEnumerable<Guid> aircraftIds, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/v1/internal/fleet/aircraft/batch", aircraftIds.ToList(), ct);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<Dictionary<Guid, AircraftBasicDto>>(ct) ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get aircraft batch");
            return new Dictionary<Guid, AircraftBasicDto>();
        }
    }

    public async Task<bool> CheckAircraftAvailabilityAsync(
        Guid aircraftId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        // Do NOT swallow HttpRequestException — a network failure to Fleet should NOT
        // silently look like "aircraft blocked". Let it propagate so the service layer
        // can return a "Could not reach the Fleet service" error to the caller.
        return await _http.GetFromJsonAsync<bool>(
            $"api/v1/internal/fleet/aircraft/{aircraftId}/availability-check" +
            $"?start={start:O}&end={end:O}", ct);
    }

    public async Task<int> GetAircraftCountByCompanyAsync(
        Guid companyId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<int>(
                $"api/v1/internal/fleet/aircraft/count/company/{companyId}", ct);
        }
        catch { return 0; }
    }

    public async Task<List<AircraftBasicDto>> GetAircraftsByCompanyAsync(
        Guid companyId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<AircraftBasicDto>>(
                $"api/v1/internal/fleet/aircraft/company/{companyId}", ct) ?? new();
        }
        catch { return new List<AircraftBasicDto>(); }
    }

    public async Task<int> GetTotalAircraftCountAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<int>("api/v1/internal/fleet/aircraft/count", ct); }
        catch { return 0; }
    }

    public async Task<int> GetTotalAirportsCountAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<int>("api/v1/internal/fleet/airports/count", ct); }
        catch { return 0; }
    }

    public Task<Guid> BlockAircraftAvailabilityAsync(
        Guid aircraftId, Guid? bookingId, DateTime start, DateTime end,
        string availabilityType, string? reason, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Availability blocking is handled via RabbitMQ events, not HTTP.");
    }
}
