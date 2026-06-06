using Booking.Application.Contracts;
using Booking.Application.DTOs;
using Booking.Application.Interfaces;
using Booking.Application.Services;
using Booking.Domain.Enums;
using FluentAssertions;
using Moq;
using Shared.Contracts.Fleet;
using Shared.Contracts.Fleet.DTOs;
using Shared.Contracts.Users;
using Shared.Contracts.Users.DTOs;

namespace Booking.Tests.Services;

/// <summary>
/// Tests for BookingService — demonstrates cross-module boundary enforcement.
/// Every dependency on Fleet or Users is mocked through module API interfaces,
/// proving that the Booking module communicates with other modules ONLY via explicit APIs.
/// IBookingEventPublisher is used for publishing booking lifecycle events via RabbitMQ.
/// </summary>
public class BookingServiceTests
{
    private readonly Mock<IBookingUOW> _uowMock;
    private readonly Mock<IBookingEventPublisher> _eventPublisherMock;
    private readonly Mock<IFleetModuleApi> _fleetApiMock;
    private readonly Mock<IUsersModuleApi> _usersApiMock;
    private readonly Mock<IBookingRepository> _bookingRepoMock;
    private readonly Mock<IReviewRepository> _reviewRepoMock;
    private readonly BookingService _sut;

    // Test constants
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _aircraftId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    public BookingServiceTests()
    {
        _uowMock = new Mock<IBookingUOW>();
        _eventPublisherMock = new Mock<IBookingEventPublisher>();
        _fleetApiMock = new Mock<IFleetModuleApi>();
        _usersApiMock = new Mock<IUsersModuleApi>();
        _bookingRepoMock = new Mock<IBookingRepository>();
        _reviewRepoMock = new Mock<IReviewRepository>();

        _uowMock.Setup(u => u.BookingRepository).Returns(_bookingRepoMock.Object);
        _uowMock.Setup(u => u.ReviewRepository).Returns(_reviewRepoMock.Object);

        _sut = new BookingService(_uowMock.Object, _eventPublisherMock.Object, _fleetApiMock.Object, _usersApiMock.Object);
    }

    #region RequestBookingAsync

    [Fact]
    public async Task RequestBookingAsync_ValidRequest_CreatesBookingAndBlocksAvailability()
    {
        // Arrange
        var dto = new CreateBookingDto
        {
            AircraftId = _aircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            Purpose = "Training flight"
        };

        SetupSuccessfulBookingMocks(dto);

        // Act
        var result = await _sut.RequestBookingAsync(dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(EBookingStatus.Requested);
        result.AircraftName.Should().Contain("ES-TCA");
        result.PilotName.Should().Contain("John");

        // Verify cross-module interactions via module API interfaces
        _usersApiMock.Verify(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _fleetApiMock.Verify(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _fleetApiMock.Verify(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _usersApiMock.Verify(u => u.CheckUserLicenseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _eventPublisherMock.Verify(m => m.PublishBookingCreatedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);

        // Verify UOW was used to persist
        _bookingRepoMock.Verify(r => r.Add(It.IsAny<Domain.Entities.Booking>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RequestBookingAsync_UserNotFound_ThrowsInvalidOperation()
    {
        // Arrange — Users module returns null for the pilot
        var dto = new CreateBookingDto
        {
            AircraftId = _aircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBasicDto?)null);

        // Act & Assert
        var act = () => _sut.RequestBookingAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*user account*not found*");
    }

    [Fact]
    public async Task RequestBookingAsync_AircraftNotFound_ThrowsInvalidOperation()
    {
        // Arrange — User exists but aircraft does not
        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(_userId, "test@test.com", "John", "Doe"));

        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AircraftBasicDto?)null);

        var dto = new CreateBookingDto
        {
            AircraftId = Guid.NewGuid(),
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        // Act & Assert
        var act = () => _sut.RequestBookingAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Aircraft not found");
    }

    [Fact]
    public async Task RequestBookingAsync_AircraftNotAvailable_ThrowsInvalidOperation()
    {
        // Arrange
        var dto = new CreateBookingDto
        {
            AircraftId = _aircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(_userId, "test@test.com", "John", "Doe"));

        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AircraftBasicDto(_aircraftId, "ES-TCA", "Cessna 172", _companyId, "PPL"));

        // Fleet module says aircraft is NOT available
        _fleetApiMock.Setup(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var act = () => _sut.RequestBookingAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blocked by an existing Fleet availability row*");
    }

    [Fact]
    public async Task RequestBookingAsync_OverlappingBookingExists_ThrowsInvalidOperation()
    {
        // Arrange
        var dto = new CreateBookingDto
        {
            AircraftId = _aircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(_userId, "test@test.com", "John", "Doe"));

        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AircraftBasicDto(_aircraftId, "ES-TCA", "Cessna 172", _companyId, "PPL"));

        // Fleet says available but there's an overlapping booking in our own module
        _fleetApiMock.Setup(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(true);

        // Act & Assert
        var act = () => _sut.RequestBookingAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*overlaps this window*");
    }

    [Fact]
    public async Task RequestBookingAsync_PilotLicenseInvalid_ThrowsInvalidOperation()
    {
        // Arrange
        var dto = new CreateBookingDto
        {
            AircraftId = _aircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(_userId, "test@test.com", "John", "Doe"));

        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AircraftBasicDto(_aircraftId, "ES-TCA", "Cessna 172", _companyId, "CPL"));

        _fleetApiMock.Setup(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(false);

        // Users module says pilot does NOT have valid license
        _usersApiMock.Setup(u => u.CheckUserLicenseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var act = () => _sut.RequestBookingAsync(dto, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*valid *pilot license*");
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_PilotCancelsOwnBooking_SucceedsAndPublishesEvent()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = CreateBookingEntity(bookingId, EBookingStatus.Requested);

        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(bookingId))
            .ReturnsAsync(booking);

        // Mock MapToDtoAsync dependencies
        SetupMapToDtoMocks(booking);

        // Act
        var result = await _sut.CancelAsync(bookingId, _userId);

        // Assert
        result.Should().NotBeNull();
        booking.Status.Should().Be(EBookingStatus.Cancelled);
        booking.CancelledAt.Should().NotBeNull();

        _eventPublisherMock.Verify(m => m.PublishBookingCancelledAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_DifferentUser_ThrowsUnauthorized()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = CreateBookingEntity(bookingId, EBookingStatus.Requested);

        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(bookingId))
            .ReturnsAsync(booking);

        var anotherUserId = Guid.NewGuid();

        // Act & Assert
        var act = () => _sut.CancelAsync(bookingId, anotherUserId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*only cancel your own*");
    }

    [Fact]
    public async Task CancelAsync_CompletedBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = CreateBookingEntity(bookingId, EBookingStatus.Completed);

        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(bookingId))
            .ReturnsAsync(booking);

        // Act & Assert
        var act = () => _sut.CancelAsync(bookingId, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Completed bookings cannot be cancelled*");
    }

    #endregion

    #region ValidateBookingAsync

    [Fact]
    public async Task ValidateBookingAsync_NoOverlapAndAvailable_ReturnsTrue()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(5);
        var end = DateTime.UtcNow.AddDays(6);

        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(
                _aircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(false);

        _fleetApiMock.Setup(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ValidateBookingAsync(_aircraftId, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateBookingAsync_HasOverlap_ReturnsFalse()
    {
        // Arrange
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(
                _aircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ValidateBookingAsync(_aircraftId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6));

        // Assert — should return false without even querying Fleet
        result.Should().BeFalse();
        _fleetApiMock.Verify(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Helper methods

    private void SetupSuccessfulBookingMocks(CreateBookingDto dto)
    {
        // Users module: user exists
        _usersApiMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(_userId, "john@example.com", "John", "Doe"));

        // Fleet module: aircraft exists
        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AircraftBasicDto(_aircraftId, "ES-TCA", "Cessna 172", _companyId, "PPL"));

        // Fleet module: aircraft is available
        _fleetApiMock.Setup(f => f.CheckAircraftAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Booking module: no overlapping bookings
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(false);

        // Users module: pilot has valid license
        _usersApiMock.Setup(u => u.CheckUserLicenseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // UOW save
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Booking reload after creation
        _bookingRepoMock.Setup(r => r.GetByIdWithIncludesAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
            .ReturnsAsync((Guid id, Guid? compId, Guid? uId) =>
            {
                return new Domain.Entities.Booking
                {
                    AircraftId = dto.AircraftId,
                    PilotId = _userId,
                    CompanyId = _companyId,
                    StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc),
                    EndDateTime = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc),
                    Status = EBookingStatus.Requested,
                    TotalAmount = 0m,
                    CreatedAt = DateTime.UtcNow
                };
            });
    }

    private void SetupMapToDtoMocks(Domain.Entities.Booking booking)
    {
        _fleetApiMock.Setup(f => f.GetAircraftByIdAsync(booking.AircraftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AircraftBasicDto(booking.AircraftId, "ES-TCA", "Cessna 172", _companyId, "PPL"));

        _usersApiMock.Setup(u => u.GetUserByIdAsync(booking.PilotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicDto(booking.PilotId, "john@example.com", "John", "Doe"));
    }

    private Domain.Entities.Booking CreateBookingEntity(Guid bookingId, EBookingStatus status)
    {
        var booking = new Domain.Entities.Booking
        {
            AircraftId = _aircraftId,
            PilotId = _userId,
            CompanyId = _companyId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            Status = status,
            TotalAmount = 500m,
            CreatedAt = DateTime.UtcNow
        };

        // Use reflection to set the Id since BaseEntity auto-generates it
        typeof(Shared.Kernel.Domain.BaseEntity).GetProperty("Id")!
            .SetValue(booking, bookingId);

        return booking;
    }

    #endregion
}
