using App.Domain.Entities;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IReviewRepository : IBaseRepository<Review>
{
    Task<IEnumerable<Review>> GetAllWithIncludesAsync();
    Task<IEnumerable<Review>> GetByAircraftIdAsync(Guid aircraftId);
    Task<Review?> GetByIdWithIncludesAsync(Guid id);
    Task<Review?> GetByBookingIdAsync(Guid bookingId);
    Task<Review?> GetByIdTrackingAsync(Guid id);
    Task<double> GetAverageRatingForAircraftAsync(Guid aircraftId);
}
