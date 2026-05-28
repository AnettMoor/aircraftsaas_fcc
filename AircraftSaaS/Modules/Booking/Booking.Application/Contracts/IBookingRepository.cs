using Booking.Domain.Entities;
using Booking.Domain.Enums;
using Shared.Kernel.DAL;

namespace Booking.Application.Contracts;

/// <summary>
/// Booking-module repository interface.
/// Cross-module validation methods (HasValidLicenseAsync, HasInsuranceCoverageAsync, etc.)
/// have been removed — those checks now happen in the service layer via MediatR.
/// </summary>
public interface IBookingRepository : IBaseRepository<Booking.Domain.Entities.Booking>
{
    Task<Booking.Domain.Entities.Booking?> GetByIdWithIncludesAsync(Guid id, Guid? companyId = null, Guid? userId = null);
    Task<IEnumerable<Booking.Domain.Entities.Booking>> GetAllForPilotAsync(Guid userId);
    Task<IEnumerable<Booking.Domain.Entities.Booking>> GetAllForCompanyAsync(Guid companyId);
    Task<bool> HasOverlappingBookingsAsync(Guid aircraftId, DateTime start, DateTime end, Guid? excludeBookingId = null);
    Task<Booking.Domain.Entities.Booking?> GetByIdTrackingWithIncludesAsync(Guid id, Guid? companyId = null);
    Task<Booking.Domain.Entities.Booking?> GetByIdTrackingWithPaymentsAsync(Guid id);
    Task<Booking.Domain.Entities.Booking?> GetByIdTrackingAsync(Guid id);
    Task<Booking.Domain.Entities.Booking?> GetByIdForPilotAsync(Guid bookingId, Guid pilotId);
    Task<IEnumerable<Booking.Domain.Entities.Booking>> GetByAircraftIdAsync(Guid aircraftId);
    void AddPayment(Payment payment);
    
    // Cross-module API support methods
    Task<int> CountByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
}
