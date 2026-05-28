using Booking.Application.Contracts;
using Booking.Application.DTOs;
using Booking.Application.Services;
using Booking.Domain.Entities;
using Booking.Domain.Enums;
using FluentAssertions;
using Moq;
using Shared.Contracts.Fleet;
using Shared.Contracts.Fleet.DTOs;
using Shared.Contracts.Users;
using Shared.Contracts.Users.DTOs;

namespace Booking.Tests.Services;

/// <summary>
/// Tests for ReviewService — validates review creation rules and cross-module DTO enrichment.
/// Reviews can only be created for completed bookings, and display data (aircraft name, author name)
/// is fetched from Fleet and Users modules via explicit module API interfaces.
/// </summary>
public class ReviewServiceTests
{
    private readonly Mock<IBookingUOW> _uowMock;
    private readonly Mock<IFleetModuleApi> _fleetApiMock;
    private readonly Mock<IUsersModuleApi> _usersApiMock;
    private readonly Mock<IBookingRepository> _bookingRepoMock;
    private readonly Mock<IReviewRepository> _reviewRepoMock;
    private readonly ReviewService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _aircraftId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    public ReviewServiceTests()
    {
        _uowMock = new Mock<IBookingUOW>();
        _fleetApiMock = new Mock<IFleetModuleApi>();
        _usersApiMock = new Mock<IUsersModuleApi>();
        _bookingRepoMock = new Mock<IBookingRepository>();
        _reviewRepoMock = new Mock<IReviewRepository>();

        _uowMock.Setup(u => u.BookingRepository).Returns(_bookingRepoMock.Object);
        _uowMock.Setup(u => u.ReviewRepository).Returns(_reviewRepoMock.Object);

        _sut = new ReviewService(_uowMock.Object, _fleetApiMock.Object, _usersApiMock.Object);
    }

    #region CreateReviewAsync

    [Fact]
    public async Task CreateReviewAsync_CompletedBooking_CreatesReview()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = CreateBookingEntity(bookingId, EBookingStatus.Completed);

        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(bookingId, _userId))
            .ReturnsAsync(booking);

        _reviewRepoMock.Setup(r => r.GetByBookingIdAsync(bookingId))
            .ReturnsAsync((Review?)null);

        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // After creation, reload the review
        _reviewRepoMock.Setup(r => r.GetByIdWithIncludesAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review
            {
                AircraftId = _aircraftId,
                BookingId = bookingId,
                AuthorId = _userId,
                Rating = 4,
                Comment = new Shared.Kernel.Domain.LangStr("Great aircraft"),
                ReviewType = new Shared.Kernel.Domain.LangStr("Aircraft"),
                ReviewedAt = DateTime.UtcNow,
                IsVerifiedBooking = true
            });

        SetupCrossModuleMocks();

        var dto = new CreateReviewDto
        {
            AircraftId = _aircraftId,
            BookingId = bookingId,
            Rating = 4,
            Comment = "Great aircraft",
            ReviewType = "Aircraft"
        };

        // Act
        var result = await _sut.CreateReviewAsync(dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Rating.Should().Be(4);
        result.AircraftName.Should().Be("ES-TCA");
        result.AuthorName.Should().Contain("John");
        result.IsVerifiedBooking.Should().BeTrue();

        _reviewRepoMock.Verify(r => r.Add(It.Is<Review>(rev =>
            rev.Rating == 4 &&
            rev.AircraftId == _aircraftId &&
            rev.BookingId == bookingId &&
            rev.IsVerifiedBooking)), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateReviewAsync_BookingNotCompleted_ThrowsInvalidOperation()
    {
        // Arrange — booking exists but is not yet completed
        var bookingId = Guid.NewGuid();
        var booking = CreateBookingEntity(bookingId, EBookingStatus.Approved);

        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(bookingId, _userId))
            .ReturnsAsync(booking);

        var dto = new CreateReviewDto
        {
            AircraftId = _aircraftId,
            BookingId = bookingId,
            Rating = 5,
            Comment = "Good"
        };

        // Act & Assert
        var act = () => _sut.CreateReviewAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only review completed bookings*");
    }

    [Fact]
    public async Task CreateReviewAsync_BookingNotFound_ThrowsInvalidOperation()
    {
        // Arrange — booking does not exist or doesn't belong to user
        var bookingId = Guid.NewGuid();
        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(bookingId, _userId))
            .ReturnsAsync((Domain.Entities.Booking?)null);

        var dto = new CreateReviewDto
        {
            AircraftId = _aircraftId,
            BookingId = bookingId,
            Rating = 3
        };

        // Act & Assert
        var act = () => _sut.CreateReviewAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid booking*");
    }

    [Fact]
    public async Task CreateReviewAsync_ReviewAlreadyExists_ThrowsInvalidOperation()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = CreateBookingEntity(bookingId, EBookingStatus.Completed);

        _bookingRepoMock.Setup(r => r.GetByIdForPilotAsync(bookingId, _userId))
            .ReturnsAsync(booking);

        // A review already exists for this booking
        _reviewRepoMock.Setup(r => r.GetByBookingIdAsync(bookingId))
            .ReturnsAsync(new Review
            {
                AircraftId = _aircraftId,
                BookingId = bookingId,
                AuthorId = _userId,
                Rating = 5
            });

        var dto = new CreateReviewDto
        {
            AircraftId = _aircraftId,
            BookingId = bookingId,
            Rating = 4
        };

        // Act & Assert
        var act = () => _sut.CreateReviewAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*review already exists*");
    }

    #endregion

    #region GetReviewByBookingIdAsync

    [Fact]
    public async Task GetReviewByBookingIdAsync_ReviewExists_ReturnsEnrichedDto()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var review = new Review
        {
            AircraftId = _aircraftId,
            BookingId = bookingId,
            AuthorId = _userId,
            Rating = 5,
            Comment = new Shared.Kernel.Domain.LangStr("Excellent"),
            ReviewedAt = DateTime.UtcNow,
            IsVerifiedBooking = true
        };

        _reviewRepoMock.Setup(r => r.GetByBookingIdAsync(bookingId))
            .ReturnsAsync(review);

        SetupCrossModuleMocks();

        // Act
        var result = await _sut.GetReviewByBookingIdAsync(bookingId);

        // Assert
        result.Should().NotBeNull();
        result!.Rating.Should().Be(5);
        result.AircraftName.Should().Be("ES-TCA");
        result.AuthorName.Should().Be("John Doe");
        result.Comment.Should().Be("Excellent");
    }

    [Fact]
    public async Task GetReviewByBookingIdAsync_NoReview_ReturnsNull()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        _reviewRepoMock.Setup(r => r.GetByBookingIdAsync(bookingId))
            .ReturnsAsync((Review?)null);

        // Act
        var result = await _sut.GetReviewByBookingIdAsync(bookingId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteReviewAsync

    [Fact]
    public async Task DeleteReviewAsync_AuthorDeletes_SoftDeletesReview()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var review = new Review
        {
            AircraftId = _aircraftId,
            BookingId = Guid.NewGuid(),
            AuthorId = _userId,
            Rating = 3
        };

        _reviewRepoMock.Setup(r => r.GetByIdTrackingAsync(reviewId))
            .ReturnsAsync(review);

        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _sut.DeleteReviewAsync(reviewId, _userId);

        // Assert
        review.IsDeleted.Should().BeTrue();
        review.DeletedBy.Should().Be(_userId.ToString());
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteReviewAsync_NonAuthorNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var review = new Review
        {
            AircraftId = _aircraftId,
            BookingId = Guid.NewGuid(),
            AuthorId = _userId,
            Rating = 3
        };

        _reviewRepoMock.Setup(r => r.GetByIdTrackingAsync(reviewId))
            .ReturnsAsync(review);

        var anotherUser = Guid.NewGuid();

        // Act & Assert
        var act = () => _sut.DeleteReviewAsync(reviewId, anotherUser);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the review author*");
    }

    #endregion

    #region Helper methods

    private void SetupCrossModuleMocks()
    {
        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AircraftBasicDto(_aircraftId, "ES-TCA", "Cessna 172", _companyId, "PPL"));

        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(_userId, "john@example.com", "John", "Doe"));
    }

    private Domain.Entities.Booking CreateBookingEntity(Guid bookingId, EBookingStatus status)
    {
        var booking = new Domain.Entities.Booking
        {
            AircraftId = _aircraftId,
            PilotId = _userId,
            CompanyId = _companyId,
            StartDateTime = DateTime.UtcNow.AddDays(-5),
            EndDateTime = DateTime.UtcNow.AddDays(-4),
            Status = status,
            TotalAmount = 400m,
            CreatedAt = DateTime.UtcNow.AddDays(-6)
        };

        typeof(Shared.Kernel.Domain.BaseEntity).GetProperty("Id")!
            .SetValue(booking, bookingId);

        return booking;
    }

    #endregion
}
