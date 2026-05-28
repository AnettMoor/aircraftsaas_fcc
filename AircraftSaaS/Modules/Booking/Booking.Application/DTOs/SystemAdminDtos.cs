using Booking.Domain.Enums;
using Shared.Contracts.Common;

namespace Booking.Application.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Bookings (system-wide)
// ─────────────────────────────────────────────────────────────────────────────

public class SystemAdminBookingDto
{
    public Guid BookingId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string AircraftRegistration { get; set; } = default!;
    public string PilotEmail { get; set; } = default!;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public EBookingStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingsListDto
{
    public PagedResult<SystemAdminBookingDto> Bookings { get; set; } = new();
    public IEnumerable<CompanySelectItemDto> Companies { get; set; } = new List<CompanySelectItemDto>();
}
