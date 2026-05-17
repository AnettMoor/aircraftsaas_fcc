using App.Application.DTOs;
using App.Application.Services;
using App.Domain.Contracts;
using App.Domain.Entities;
using App.Domain.Enums;
using Base.Domain;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class ReviewServiceTests
{
    private readonly Mock<IAppUOW> _uowMock;
    private readonly Mock<IReviewRepository> _reviewRepoMock;
    private readonly Mock<IBookingRepository> _bookingRepoMock;
    private readonly ReviewService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AircraftId = Guid.NewGuid();
    private static readonly Guid BookingId = Guid.NewGuid();

    public ReviewServiceTests()
    {
        _uowMock = new Mock<IAppUOW>();
        _reviewRepoMock = new Mock<IReviewRepository>();
        _bookingRepoMock = new Mock<IBookingRepository>();

        _uowMock.Setup(u => u.ReviewRepository).Returns(_reviewRepoMock.Object);
        _uowMock.Setup(u => u.BookingRepository).Returns(_bookingRepoMock.Object);

        _sut = new ReviewService(_uowMock.Object);
    }

    [Fact]
    public async Task CreateReviewAsync_ValidCompletedBooking_CreatesReview()
    {
        // Arrange
        var booking = new Booking
        {
            Id = BookingId,
            AircraftId = AircraftId,
            PilotId = UserId,
            Status = EBookingStatus.Completed,
            StartDateTime = DateTime.UtcNow.AddDays(-1),
            EndDateTime = DateTime.UtcNow.AddDays(-1).AddHours(2),
            CompanyId = Guid.NewGuid(),
            TotalAmount = 300m
        };
        var dto = new CreateReviewDto
        {
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 5,
            Comment = "Excellent aircraft"
        };

        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(BookingId, UserId)).ReturnsAsync(booking);
        _reviewRepoMock.Setup(r => r.GetByBookingIdAsync(BookingId)).ReturnsAsync((Review?)null);
        _reviewRepoMock.Setup(r => r.GetByIdWithIncludesAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Review
            {
                AircraftId = AircraftId,
                BookingId = BookingId,
                AuthorId = UserId,
                Rating = 5,
                Comment = new LangStr("Excellent aircraft"),
                ReviewedAt = DateTime.UtcNow,
                IsVerifiedBooking = true
            });

        // Act
        var result = await _sut.CreateReviewAsync(dto, UserId);

        // Assert
        result.Should().NotBeNull();
        result.Rating.Should().Be(5);
        result.IsVerifiedBooking.Should().BeTrue();
        _reviewRepoMock.Verify(r => r.Add(It.Is<Review>(rev =>
            rev.AuthorId == UserId &&
            rev.Rating == 5 &&
            rev.IsVerifiedBooking
        )), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateReviewAsync_BookingNotCompletedOrWrongUser_ThrowsInvalidOperation()
    {
        // Arrange
        var dto = new CreateReviewDto
        {
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 4
        };
        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(BookingId, UserId)).ReturnsAsync((Booking?)null);

        // Act
        var act = () => _sut.CreateReviewAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid booking*");
    }

    [Fact]
    public async Task CreateReviewAsync_BookingNotCompleted_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = new Booking
        {
            Id = BookingId,
            AircraftId = AircraftId,
            PilotId = UserId,
            Status = EBookingStatus.Approved, // not completed
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            CompanyId = Guid.NewGuid(),
            TotalAmount = 300m
        };
        var dto = new CreateReviewDto
        {
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 3
        };
        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(BookingId, UserId)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.CreateReviewAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only review completed bookings*");
    }

    [Fact]
    public async Task CreateReviewAsync_DuplicateReview_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = new Booking
        {
            Id = BookingId,
            AircraftId = AircraftId,
            PilotId = UserId,
            Status = EBookingStatus.Completed,
            StartDateTime = DateTime.UtcNow.AddDays(-1),
            EndDateTime = DateTime.UtcNow.AddDays(-1).AddHours(2),
            CompanyId = Guid.NewGuid(),
            TotalAmount = 300m
        };
        var existingReview = new Review
        {
            BookingId = BookingId,
            AircraftId = AircraftId,
            AuthorId = UserId,
            Rating = 4
        };
        var dto = new CreateReviewDto
        {
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 5
        };
        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(BookingId, UserId)).ReturnsAsync(booking);
        _reviewRepoMock.Setup(r => r.GetByBookingIdAsync(BookingId)).ReturnsAsync(existingReview);

        // Act
        var act = () => _sut.CreateReviewAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*review already exists*");
    }

    // ===================== UpdateReviewAsync — IDOR =====================

    [Fact]
    public async Task UpdateReviewAsync_OtherUserNotAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var review = new Review
        {
            Id = reviewId,
            AuthorId = UserId,
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 3
        };
        var otherUserId = Guid.NewGuid();
        _reviewRepoMock.Setup(r => r.GetByIdTrackingAsync(reviewId)).ReturnsAsync(review);

        // Act
        var act = () => _sut.UpdateReviewAsync(reviewId, new UpdateReviewDto { Rating = 5 }, otherUserId, isAdmin: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the review author*");
    }

    [Fact]
    public async Task UpdateReviewAsync_AdminCanUpdateOthersReview()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var review = new Review
        {
            Id = reviewId,
            AuthorId = UserId,
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 3
        };
        var adminId = Guid.NewGuid();
        _reviewRepoMock.Setup(r => r.GetByIdTrackingAsync(reviewId)).ReturnsAsync(review);

        // Act
        var result = await _sut.UpdateReviewAsync(reviewId, new UpdateReviewDto { Rating = 1 }, adminId, isAdmin: true);

        // Assert
        result.Rating.Should().Be(1);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ===================== DeleteReviewAsync — IDOR =====================

    [Fact]
    public async Task DeleteReviewAsync_OtherUserNotAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var review = new Review
        {
            Id = reviewId,
            AuthorId = UserId,
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 3
        };
        var otherUserId = Guid.NewGuid();
        _reviewRepoMock.Setup(r => r.GetByIdTrackingAsync(reviewId)).ReturnsAsync(review);

        // Act
        var act = () => _sut.DeleteReviewAsync(reviewId, otherUserId, isAdmin: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the review author*");
    }

    [Fact]
    public async Task DeleteReviewAsync_Author_CanSoftDelete()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var review = new Review
        {
            Id = reviewId,
            AuthorId = UserId,
            AircraftId = AircraftId,
            BookingId = BookingId,
            Rating = 4
        };
        _reviewRepoMock.Setup(r => r.GetByIdTrackingAsync(reviewId)).ReturnsAsync(review);

        // Act
        await _sut.DeleteReviewAsync(reviewId, UserId, isAdmin: false);

        // Assert
        review.IsDeleted.Should().BeTrue();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
