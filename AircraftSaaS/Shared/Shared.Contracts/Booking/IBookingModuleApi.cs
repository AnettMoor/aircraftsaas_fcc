using Shared.Contracts.Booking.DTOs;

namespace Shared.Contracts.Booking;

public interface IBookingModuleApi
{
    Task<int> GetBookingCountByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<int> GetBookingCountByUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<BookingBasicDto>> GetBookingsByAircraftAsync(Guid aircraftId, CancellationToken ct = default);
    Task<int> GetTotalBookingsCountAsync(CancellationToken ct = default);
}
