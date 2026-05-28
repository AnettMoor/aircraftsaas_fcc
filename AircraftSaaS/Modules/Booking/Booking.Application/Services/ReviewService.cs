using Booking.Application.Contracts;
using Booking.Application.DTOs;
using Booking.Application.Interfaces;
using Booking.Domain.Entities;
using Booking.Domain.Enums;
using Shared.Contracts.Fleet;
using Shared.Contracts.Users;
using Shared.Kernel.Domain;

namespace Booking.Application.Services;

internal sealed class ReviewService : IReviewService
{
    private readonly IBookingUOW _uow;
    private readonly IFleetModuleApi _fleetApi;
    private readonly IUsersModuleApi _usersApi;

    public ReviewService(IBookingUOW uow, IFleetModuleApi fleetApi, IUsersModuleApi usersApi)
    {
        _uow = uow;
        _fleetApi = fleetApi;
        _usersApi = usersApi;
    }

    public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync()
    {
        var reviews = await _uow.ReviewRepository.GetAllAsync();
        return await MapToDtosAsync(reviews);
    }

    public async Task<IEnumerable<ReviewDto>> GetReviewsByAircraftIdAsync(Guid aircraftId)
    {
        var reviews = await _uow.ReviewRepository.GetByAircraftIdAsync(aircraftId);
        return await MapToDtosAsync(reviews);
    }

    public async Task<ReviewDto?> GetReviewByIdAsync(Guid id)
    {
        var review = await _uow.ReviewRepository.GetByIdWithIncludesAsync(id);
        return review == null ? null : await MapToDtoAsync(review);
    }

    public async Task<ReviewDto?> GetReviewByBookingIdAsync(Guid bookingId)
    {
        var review = await _uow.ReviewRepository.GetByBookingIdAsync(bookingId);
        return review == null ? null : await MapToDtoAsync(review);
    }

    public async Task<ReviewDto> CreateReviewAsync(CreateReviewDto dto, Guid userId)
    {
        // Verify the booking is completed and belongs to the user
        var booking = await _uow.BookingRepository.GetByIdForPilotAsync(dto.BookingId, userId);

        if (booking == null)
        {
            throw new InvalidOperationException("Invalid booking or you don't have permission to review this booking");
        }

        if (booking.Status != EBookingStatus.Completed)
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

        // Reload with includes
        var created = await _uow.ReviewRepository.GetByIdWithIncludesAsync(review.Id);
        return await MapToDtoAsync(created!);
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
        return await MapToDtoAsync(review);
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

    /// <summary>
    /// Maps a Review entity to ReviewDto, enriching with cross-module data via module APIs.
    /// Since the Booking module has NO navigation properties to Aircraft or Author (User),
    /// we fetch display names from the Fleet and Users modules.
    /// </summary>
    private async Task<ReviewDto> MapToDtoAsync(Review review)
    {
        // Fetch aircraft name from Fleet module
        var aircraftName = "Unknown";
        var aircraft = await _fleetApi.GetAircraftByIdAsync(review.AircraftId);
        if (aircraft != null)
        {
            aircraftName = aircraft.Registration;
        }

        // Fetch author name from Users module
        var authorName = "Unknown";
        var author = await _usersApi.GetUserByIdAsync(review.AuthorId);
        if (author != null)
        {
            authorName = $"{author.FirstName} {author.LastName}";
        }

        return new ReviewDto
        {
            Id = review.Id,
            AircraftId = review.AircraftId,
            AircraftName = aircraftName,
            BookingId = review.BookingId,
            AuthorId = review.AuthorId,
            AuthorName = authorName,
            Rating = review.Rating,
            Comment = review.Comment?.ToString(),
            ReviewType = review.ReviewType?.ToString(),
            ReviewedAt = review.ReviewedAt,
            IsVerifiedBooking = review.IsVerifiedBooking
        };
    }

    /// <summary>
    /// Batch-maps a collection of Review entities to ReviewDtos.
    /// Uses batch cross-module API calls to avoid N+1 problems.
    /// </summary>
    private async Task<List<ReviewDto>> MapToDtosAsync(IEnumerable<Review> reviews)
    {
        var reviewList = reviews.ToList();
        if (reviewList.Count == 0)
            return new List<ReviewDto>();

        // Collect all unique IDs
        var aircraftIds = reviewList.Select(r => r.AircraftId).Distinct();
        var authorIds = reviewList.Select(r => r.AuthorId).Distinct();

        // Batch-fetch cross-module data (2 calls total instead of 2N)
        var aircraftMap = await _fleetApi.GetAircraftsByIdsAsync(aircraftIds);
        var authorMap = await _usersApi.GetUsersByIdsAsync(authorIds);

        return reviewList.Select(review =>
        {
            var aircraftName = "Unknown";
            if (aircraftMap.TryGetValue(review.AircraftId, out var aircraft))
            {
                aircraftName = aircraft.Registration;
            }

            var authorName = "Unknown";
            if (authorMap.TryGetValue(review.AuthorId, out var author))
            {
                authorName = $"{author.FirstName} {author.LastName}";
            }

            return new ReviewDto
            {
                Id = review.Id,
                AircraftId = review.AircraftId,
                AircraftName = aircraftName,
                BookingId = review.BookingId,
                AuthorId = review.AuthorId,
                AuthorName = authorName,
                Rating = review.Rating,
                Comment = review.Comment?.ToString(),
                ReviewType = review.ReviewType?.ToString(),
                ReviewedAt = review.ReviewedAt,
                IsVerifiedBooking = review.IsVerifiedBooking
            };
        }).ToList();
    }
}
