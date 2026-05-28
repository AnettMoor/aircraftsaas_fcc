using Asp.Versioning;
using Booking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Booking;
using Shared.Contracts.Booking.DTOs;

namespace Booking.WebHost.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/booking")]
[ApiController]
public class InternalBookingController : ControllerBase
{
    private readonly IBookingModuleApi _bookingApi;
    private readonly ISystemAdminBookingService _adminService;

    public InternalBookingController(
        IBookingModuleApi bookingApi,
        ISystemAdminBookingService adminService)
    {
        _bookingApi = bookingApi;
        _adminService = adminService;
    }

    // ── IBookingModuleApi endpoints (for Fleet service) ─────────────

    [HttpGet("count/company/{companyId:guid}")]
    public async Task<ActionResult<int>> GetBookingCountByCompany(Guid companyId)
        => Ok(await _bookingApi.GetBookingCountByCompanyAsync(companyId));

    [HttpGet("count/user/{userId:guid}")]
    public async Task<ActionResult<int>> GetBookingCountByUser(Guid userId)
        => Ok(await _bookingApi.GetBookingCountByUserAsync(userId));

    [HttpGet("aircraft/{aircraftId:guid}")]
    public async Task<ActionResult<List<BookingBasicDto>>> GetBookingsByAircraft(Guid aircraftId)
        => Ok(await _bookingApi.GetBookingsByAircraftAsync(aircraftId));

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetTotalBookingsCount()
        => Ok(await _bookingApi.GetTotalBookingsCountAsync());

    // ── Admin endpoints (for WebApp SystemAdmin panel) ──────────────

    [HttpGet("admin/bookings")]
    public async Task<IActionResult> GetAllBookings(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _adminService.GetAllBookingsAsync(search, status, tenantId, page, pageSize));
}
