using Booking.Application.DTOs;

namespace Booking.Application.Interfaces;

public interface ISystemAdminBookingService
{
    // ── All Bookings (system-wide) ───────────────────────────────────────────
    Task<BookingsListDto> GetAllBookingsAsync(string? search, string? status, Guid? tenantId, int page, int pageSize);
}
