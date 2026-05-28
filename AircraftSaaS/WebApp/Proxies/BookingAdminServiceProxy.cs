using System.Net.Http.Json;
using Booking.Application.DTOs;
using Booking.Application.Interfaces;

namespace WebApp.Proxies;

/// <summary>
/// HTTP proxy for ISystemAdminBookingService — delegates all booking admin operations
/// to the Booking microservice via REST calls to internal endpoints.
/// </summary>
public class BookingAdminServiceProxy : ISystemAdminBookingService
{
    private readonly HttpClient _http;
    private readonly ILogger<BookingAdminServiceProxy> _logger;

    public BookingAdminServiceProxy(HttpClient http, ILogger<BookingAdminServiceProxy> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<BookingsListDto> GetAllBookingsAsync(
        string? search, string? status, Guid? tenantId, int page, int pageSize)
    {
        try
        {
            var url = $"api/v1/internal/booking/admin/bookings?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (tenantId.HasValue) url += $"&tenantId={tenantId}";

            return await _http.GetFromJsonAsync<BookingsListDto>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all bookings from Booking service");
            return new BookingsListDto();
        }
    }
}
