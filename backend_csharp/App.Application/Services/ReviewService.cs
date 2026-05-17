using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain.Entities;
using Base.Domain;

namespace App.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IAppUOW _uow;

    public ReviewService(IAppUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync()
    {
        var reviews = await _uow.ReviewRepository.GetAllWithIncludesAsync();
        return reviews.Select(MapToDto);
    }

    public async Task<IEnumerable<ReviewDto>> GetReviewsByAircraftIdAsync(Guid aircraftId)
    {
        var reviews = await _uow.ReviewRepository.GetByAircraftIdAsync(aircraftId);
        return reviews.Select(MapToDto);
    }

    public async Task<ReviewDto?> GetReviewByIdAsync(Guid id)
    {
        var review = await _uow.ReviewRepository.GetByIdWithIncludesAsync(id);
        return review == null ? null : MapToDto(review);
    }

    public async Task<ReviewDto?> GetReviewByBookingIdAsync(Guid bookingId)
    {
        var review = await _uow.ReviewRepository.GetByBookingIdAsync(bookingId);
        return review == null ? null : MapToDto(review);
    }

    public async Task<ReviewDto> CreateReviewAsync(CreateReviewDto dto, Guid userId)
    {
        // Verify the booking is completed and belongs to the user
        var booking = await _uow.BookingRepository.GetByIdForPilotAsync(dto.BookingId, userId);

        if (booking == null)
        {
            throw new InvalidOperationException("Invalid booking or you don't have permission to review this booking");
        }

        if (booking.Status != Domain.Enums.EBookingStatus.Completed)
        {
            throw new InvalidOperationException("You can only review completed bookings");
        }

        // Check if review already exists for this booking
        var existingReview = await _uow.ReviewRepository.GetByBookingIdAsync(dto.BookingId);

        if (existingReview != null)
        {
            throw new InvalidOperationException("A review already exists for this booking");
        }

        var review = new Review
        {
            AircraftId = dto.AircraftId,
            BookingId = dto.BookingId,
            AuthorId = userId,
            Rating = dto.Rating,
            Comment = dto.Comment != null ? new LangStr(dto.Comment) : null,
            ReviewType = dto.ReviewType != null ? new LangStr(dto.ReviewType) : null,
            ReviewedAt = DateTime.UtcNow,
            IsVerifiedBooking = true
        };

        _uow.ReviewRepository.Add(review);
        await _uow.SaveChangesAsync();

        // Reload with navigation properties
        var created = await _uow.ReviewRepository.GetByIdWithIncludesAsync(review.Id);
        return MapToDto(created!);
    }

    public async Task<ReviewDto> UpdateReviewAsync(Guid id, UpdateReviewDto dto, Guid callerId, bool isAdmin = false)
    {
        var review = await _uow.ReviewRepository.GetByIdTrackingAsync(id);

        if (review == null)
        {
            throw new InvalidOperationException("Review not found");
        }

        // IDOR protection: only the review author or a system admin can update a review
        if (!isAdmin && review.AuthorId != callerId)
        {
            throw new UnauthorizedAccessException("Only the review author or system admins can update this review");
        }

        review.Rating = dto.Rating;
        if (dto.Comment != null)
        {
            if (review.Comment == null)
                review.Comment = new LangStr(dto.Comment);
            else
                review.Comment.SetTranslation(dto.Comment);
        }
        if (dto.ReviewType != null)
        {
            if (review.ReviewType == null)
                review.ReviewType = new LangStr(dto.ReviewType);
            else
                review.ReviewType.SetTranslation(dto.ReviewType);
        }

        await _uow.SaveChangesAsync();
        return MapToDto(review);
    }

    public async Task DeleteReviewAsync(Guid id, Guid callerId, bool isAdmin = false)
    {
        var review = await _uow.ReviewRepository.GetByIdTrackingAsync(id);
        if (review == null)
        {
            throw new InvalidOperationException("Review not found");
        }

        // IDOR protection: only the review author or a system admin can delete a review
        if (!isAdmin && review.AuthorId != callerId)
        {
            throw new UnauthorizedAccessException("Only the review author or system admins can delete this review");
        }

        review.SoftDelete(callerId.ToString());
        await _uow.SaveChangesAsync();
    }

    public async Task<double> GetAverageRatingForAircraftAsync(Guid aircraftId)
    {
        return await _uow.ReviewRepository.GetAverageRatingForAircraftAsync(aircraftId);
    }

    private static ReviewDto MapToDto(Review review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            AircraftId = review.AircraftId,
            AircraftName = review.Aircraft?.RegistrationNumber ?? "Unknown",
            BookingId = review.BookingId,
            AuthorId = review.AuthorId,
            AuthorName = review.Author != null 
                ? $"{review.Author.FirstName} {review.Author.LastName}" 
                : "Unknown",
            Rating = review.Rating,
            Comment = review.Comment?.ToString(),
            ReviewType = review.ReviewType?.ToString(),
            ReviewedAt = review.ReviewedAt,
            IsVerifiedBooking = review.IsVerifiedBooking
        };
    }
}
