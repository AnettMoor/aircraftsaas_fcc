using Booking.Domain.Entities;
using Shared.Kernel.DAL;

namespace Booking.Application.Contracts;

public interface IReviewRepository : IBaseRepository<Review>
{
    Task<IEnumerable<Review>> GetAllAsync();
    Task<IEnumerable<Review>> GetByAircraftIdAsync(Guid aircraftId);
    Task<Review?> GetByIdWithIncludesAsync(Guid id);
    Task<Review?> GetByBookingIdAsync(Guid bookingId);
    Task<Review?> GetByIdTrackingAsync(Guid id);
    Task<double> GetAverageRatingForAircraftAsync(Guid aircraftId);
}
