using System.Net.Http.Json;
using Shared.Contracts.Booking;
using Shared.Contracts.Booking.DTOs;

namespace Fleet.WebHost.Proxies;

public class BookingServiceHttpClient : IBookingModuleApi
{
    private readonly HttpClient _http;
    private readonly ILogger<BookingServiceHttpClient> _logger;

    public BookingServiceHttpClient(HttpClient http, ILogger<BookingServiceHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<int> GetBookingCountByCompanyAsync(
        Guid companyId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<int>(
                $"api/v1/internal/booking/count/company/{companyId}", ct);
        }
        catch { return 0; }
    }

    public async Task<int> GetBookingCountByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<int>(
                $"api/v1/internal/booking/count/user/{userId}", ct);
        }
        catch { return 0; }
    }

    public async Task<List<BookingBasicDto>> GetBookingsByAircraftAsync(
        Guid aircraftId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<BookingBasicDto>>(
                $"api/v1/internal/booking/aircraft/{aircraftId}", ct) ?? new();
        }
        catch { return new List<BookingBasicDto>(); }
    }

    public async Task<int> GetTotalBookingsCountAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<int>("api/v1/internal/booking/count", ct); }
        catch { return 0; }
    }
}
