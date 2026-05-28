using Booking.Application.DTOs;

namespace Booking.Application.Interfaces;

public interface IReviewService
{
    Task<IEnumerable<ReviewDto>> GetAllReviewsAsync();
    Task<IEnumerable<ReviewDto>> GetReviewsByAircraftIdAsync(Guid aircraftId);
    Task<ReviewDto?> GetReviewByIdAsync(Guid id);
    Task<ReviewDto?> GetReviewByBookingIdAsync(Guid bookingId);
    Task<ReviewDto> CreateReviewAsync(CreateReviewDto dto, Guid userId);
    Task<ReviewDto> UpdateReviewAsync(Guid id, UpdateReviewDto dto, Guid callerId, bool isAdmin = false);
    Task DeleteReviewAsync(Guid id, Guid callerId, bool isAdmin = false);
    Task<double> GetAverageRatingForAircraftAsync(Guid aircraftId);
}
