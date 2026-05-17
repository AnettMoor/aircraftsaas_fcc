using App.Application.DTOs;
using App.Application.Services;
using App.Domain.Contracts;
using App.Domain.Entities;
using App.Domain.Enums;
using Base.Domain;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class BookingServiceTests
{
    private readonly Mock<IAppUOW> _uowMock;
    private readonly Mock<IBookingRepository> _bookingRepoMock;
    private readonly Mock<IAircraftRepository> _aircraftRepoMock;
    private readonly Mock<IMaintenanceRecordRepository> _maintenanceRepoMock;
    private readonly Mock<IAircraftAvailabilityRepository> _availabilityRepoMock;
    private readonly BookingService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid AircraftId = Guid.NewGuid();

    public BookingServiceTests()
    {
        _uowMock = new Mock<IAppUOW>();
        _bookingRepoMock = new Mock<IBookingRepository>();
        _aircraftRepoMock = new Mock<IAircraftRepository>();
        _maintenanceRepoMock = new Mock<IMaintenanceRecordRepository>();
        _availabilityRepoMock = new Mock<IAircraftAvailabilityRepository>();

        _uowMock.Setup(u => u.BookingRepository).Returns(_bookingRepoMock.Object);
        _uowMock.Setup(u => u.AircraftRepository).Returns(_aircraftRepoMock.Object);
        _uowMock.Setup(u => u.MaintenanceRecordRepository).Returns(_maintenanceRepoMock.Object);
        _uowMock.Setup(u => u.AircraftAvailabilityRepository).Returns(_availabilityRepoMock.Object);

        _sut = new BookingService(_uowMock.Object);
    }

    private Aircraft CreateTestAircraft() => new()
    {
        Id = AircraftId,
        RegistrationNumber = "ES-TFC",
        Make = new LangStr("Cessna"),
        Model = new LangStr("172"),
        Category = new LangStr("SingleEngineLand"),
        RequiredLicenseType = "PPL",
        HourlyRate = 150m,
        IsAvailable = true,
        CompanyId = CompanyId,
        Description = new LangStr("Test aircraft")
    };

    private Booking CreateTestBooking(EBookingStatus status = EBookingStatus.Requested) => new()
    {
        Id = Guid.NewGuid(),
        AircraftId = AircraftId,
        PilotId = UserId,
        CompanyId = CompanyId,
        StartDateTime = DateTime.UtcNow.AddDays(1),
        EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2),
        Status = status,
        TotalAmount = 300m,
        Purpose = "Training flight"
    };

    // ===================== RequestBookingAsync =====================

    [Fact]
    public async Task RequestBookingAsync_ValidRequest_CreatesBookingAndCalculatesAmount()
    {
        // Arrange
        var aircraft = CreateTestAircraft();
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(3);
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = start,
            EndDateTime = end,
            Purpose = "Sightseeing"
        };

        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(true);
        _aircraftRepoMock.Setup(r => r.GetByIdWithIncludesAsync(AircraftId, null)).ReturnsAsync(aircraft);
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
        _maintenanceRepoMock.Setup(r => r.GetScheduledForAircraftInRangeAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<MaintenanceRecord>());
        _availabilityRepoMock.Setup(r => r.HasBlockingAvailabilityAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(false);
        _bookingRepoMock.Setup(r => r.HasValidLicenseAsync(UserId, "PPL", It.IsAny<DateTime>())).ReturnsAsync(true);
        _bookingRepoMock.Setup(r => r.HasInsuranceCoverageAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(true);
        _bookingRepoMock.Setup(r => r.GetByIdWithIncludesAsync(It.IsAny<Guid>(), CompanyId, UserId))
            .ReturnsAsync((Guid id, Guid? cid, Guid? uid) => new Booking
            {
                Id = id,
                AircraftId = AircraftId,
                PilotId = UserId,
                CompanyId = CompanyId,
                StartDateTime = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                EndDateTime = DateTime.SpecifyKind(end, DateTimeKind.Utc),
                Status = EBookingStatus.Requested,
                TotalAmount = 3 * 150m,
                Purpose = "Sightseeing"
            });

        // Act
        var result = await _sut.RequestBookingAsync(dto, UserId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(EBookingStatus.Requested);
        result.TotalAmount.Should().Be(450m); // 3 hours * 150/hr
        result.Purpose.Should().Be("Sightseeing");
        _bookingRepoMock.Verify(r => r.Add(It.Is<Booking>(b => b.PilotId == UserId && b.Status == EBookingStatus.Requested)), Times.Once);
        _availabilityRepoMock.Verify(r => r.Add(It.Is<AircraftAvailability>(a => a.AircraftId == AircraftId && a.AvailabilityType == "Booked")), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task RequestBookingAsync_UserNotFound_ThrowsInvalidOperation()
    {
        // Arrange
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(false);

        // Act
        var act = () => _sut.RequestBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*user account*not found*");
    }

    [Fact]
    public async Task RequestBookingAsync_AircraftNotFound_ThrowsInvalidOperation()
    {
        // Arrange
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(true);
        _aircraftRepoMock.Setup(r => r.GetByIdWithIncludesAsync(AircraftId, null)).ReturnsAsync((Aircraft?)null);

        // Act
        var act = () => _sut.RequestBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Aircraft not found");
    }

    [Fact]
    public async Task RequestBookingAsync_AircraftNotAvailable_ThrowsInvalidOperation()
    {
        // Arrange
        var aircraft = CreateTestAircraft();
        aircraft.IsAvailable = false;
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(true);
        _aircraftRepoMock.Setup(r => r.GetByIdWithIncludesAsync(AircraftId, null)).ReturnsAsync(aircraft);

        // Act
        var act = () => _sut.RequestBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available for booking*");
    }

    [Fact]
    public async Task RequestBookingAsync_OverlappingBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var aircraft = CreateTestAircraft();
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(true);
        _aircraftRepoMock.Setup(r => r.GetByIdWithIncludesAsync(AircraftId, null)).ReturnsAsync(aircraft);
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(true);

        // Act
        var act = () => _sut.RequestBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available for the selected dates*");
    }

    [Fact]
    public async Task RequestBookingAsync_NoValidLicenseAndNoLicenseAtAll_ThrowsWithLicenseMessage()
    {
        // Arrange
        var aircraft = CreateTestAircraft();
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(true);
        _aircraftRepoMock.Setup(r => r.GetByIdWithIncludesAsync(AircraftId, null)).ReturnsAsync(aircraft);
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
        _maintenanceRepoMock.Setup(r => r.GetScheduledForAircraftInRangeAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<MaintenanceRecord>());
        _availabilityRepoMock.Setup(r => r.HasBlockingAvailabilityAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(false);
        _bookingRepoMock.Setup(r => r.HasValidLicenseAsync(UserId, "PPL", It.IsAny<DateTime>())).ReturnsAsync(false);
        _bookingRepoMock.Setup(r => r.HasAnyLicenseAsync(UserId)).ReturnsAsync(false);

        // Act
        var act = () => _sut.RequestBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must add a valid pilot license*");
    }

    [Fact]
    public async Task RequestBookingAsync_NoInsuranceCoverage_ThrowsInvalidOperation()
    {
        // Arrange
        var aircraft = CreateTestAircraft();
        var dto = new CreateBookingDto
        {
            AircraftId = AircraftId,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _bookingRepoMock.Setup(r => r.UserExistsAsync(UserId)).ReturnsAsync(true);
        _aircraftRepoMock.Setup(r => r.GetByIdWithIncludesAsync(AircraftId, null)).ReturnsAsync(aircraft);
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
        _maintenanceRepoMock.Setup(r => r.GetScheduledForAircraftInRangeAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<MaintenanceRecord>());
        _availabilityRepoMock.Setup(r => r.HasBlockingAvailabilityAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(false);
        _bookingRepoMock.Setup(r => r.HasValidLicenseAsync(UserId, "PPL", It.IsAny<DateTime>())).ReturnsAsync(true);
        _bookingRepoMock.Setup(r => r.HasInsuranceCoverageAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(false);

        // Act
        var act = () => _sut.RequestBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not have valid insurance*");
    }

    // ===================== ApproveAsync =====================

    [Fact]
    public async Task ApproveAsync_PendingBooking_SetsStatusToApproved()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(booking.Id, CompanyId)).ReturnsAsync(booking);

        // Act
        var result = await _sut.ApproveAsync(booking.Id, CompanyId);

        // Assert
        result.Status.Should().Be(EBookingStatus.Approved);
        result.ApprovedAt.Should().NotBeNull();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_NonPendingBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Approved);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(booking.Id, CompanyId)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.ApproveAsync(booking.Id, CompanyId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only requested bookings can be approved*");
    }

    [Fact]
    public async Task ApproveAsync_BookingNotFound_ThrowsInvalidOperation()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(bookingId, CompanyId)).ReturnsAsync((Booking?)null);

        // Act
        var act = () => _sut.ApproveAsync(bookingId, CompanyId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Booking not found");
    }

    // ===================== RejectAsync =====================

    [Fact]
    public async Task RejectAsync_PendingBooking_SetsStatusToRejectedWithReason()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(booking.Id, CompanyId)).ReturnsAsync(booking);

        // Act
        var result = await _sut.RejectAsync(booking.Id, CompanyId, "Maintenance scheduled");

        // Assert
        result.Status.Should().Be(EBookingStatus.Rejected);
        result.RejectionReason.Should().Be("Maintenance scheduled");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_NonPendingBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Approved);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(booking.Id, CompanyId)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.RejectAsync(booking.Id, CompanyId, "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only requested bookings can be rejected*");
    }

    // ===================== CancelAsync =====================

    [Fact]
    public async Task CancelAsync_OwnBooking_SetsStatusToCancelled()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(booking.Id)).ReturnsAsync(booking);

        // Act
        var result = await _sut.CancelAsync(booking.Id, UserId);

        // Assert
        result.Status.Should().Be(EBookingStatus.Cancelled);
        result.CancelledAt.Should().NotBeNull();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_CompletedBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Completed);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(booking.Id)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.CancelAsync(booking.Id, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Completed bookings cannot be cancelled*");
    }

    [Fact]
    public async Task CancelAsync_OtherUserNotOwner_ThrowsUnauthorized()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        var otherUserId = Guid.NewGuid();
        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(booking.Id)).ReturnsAsync(booking);
        _bookingRepoMock.Setup(r => r.IsCompanyOwnerAsync(otherUserId, CompanyId)).ReturnsAsync(false);

        // Act
        var act = () => _sut.CancelAsync(booking.Id, otherUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*can only cancel your own bookings*");
    }

    [Fact]
    public async Task CancelAsync_CompanyOwner_CanCancelOthersBooking()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        var ownerId = Guid.NewGuid();
        _bookingRepoMock.Setup(r => r.GetByIdTrackingAsync(booking.Id)).ReturnsAsync(booking);
        _bookingRepoMock.Setup(r => r.IsCompanyOwnerAsync(ownerId, CompanyId)).ReturnsAsync(true);

        // Act
        var result = await _sut.CancelAsync(booking.Id, ownerId);

        // Assert
        result.Status.Should().Be(EBookingStatus.Cancelled);
    }

    // ===================== CompleteAsync =====================

    [Fact]
    public async Task CompleteAsync_PaidBooking_SetsStatusToCompleted()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Paid);
        var aircraft = CreateTestAircraft();
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(booking.Id, CompanyId)).ReturnsAsync(booking);
        _aircraftRepoMock.Setup(r => r.GetByIdForCompanyTrackingAsync(AircraftId, CompanyId)).ReturnsAsync(aircraft);

        // Act
        var result = await _sut.CompleteAsync(booking.Id, CompanyId);

        // Assert
        result.Status.Should().Be(EBookingStatus.Completed);
        result.CompletedAt.Should().NotBeNull();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_RequestedBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(booking.Id, CompanyId)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.CompleteAsync(booking.Id, CompanyId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only paid bookings can be completed*");
    }

    // ===================== ConfirmPaymentAsync =====================

    [Fact]
    public async Task ConfirmPaymentAsync_ApprovedBooking_SetsStatusToPaid()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Approved);
        booking.TotalAmount = 300m;
        var paymentDto = new PaymentDto
        {
            PaymentMethod = "CreditCard",
            TransactionId = "TXN-12345"
        };
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithPaymentsAsync(booking.Id)).ReturnsAsync(booking);

        // Act
        var result = await _sut.ConfirmPaymentAsync(booking.Id, paymentDto, UserId);

        // Assert
        result.Status.Should().Be(EBookingStatus.Paid);
        result.PaidAt.Should().NotBeNull();
        _bookingRepoMock.Verify(r => r.AddPayment(It.Is<Payment>(p =>
            p.Amount == 300m &&
            p.PaymentMethod == "CreditCard" &&
            p.TransactionId == "TXN-12345"
        )), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_OtherUser_ThrowsUnauthorized()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Approved);
        var otherUserId = Guid.NewGuid();
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithPaymentsAsync(booking.Id)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.ConfirmPaymentAsync(booking.Id, new PaymentDto { PaymentMethod = "Card" }, otherUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*can only pay for your own bookings*");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_NotApproved_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithPaymentsAsync(booking.Id)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.ConfirmPaymentAsync(booking.Id, new PaymentDto { PaymentMethod = "Card" }, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only approved bookings can be paid*");
    }

    // ===================== UpdateBookingAsync =====================

    [Fact]
    public async Task UpdateBookingAsync_OtherUser_ThrowsUnauthorized()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Requested);
        var otherUserId = Guid.NewGuid();
        var dto = new UpdateBookingDto
        {
            Id = booking.Id,
            StartDateTime = DateTime.UtcNow.AddDays(2),
            EndDateTime = DateTime.UtcNow.AddDays(2).AddHours(3)
        };
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(dto.Id, null)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.UpdateBookingAsync(dto, otherUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*can only edit your own bookings*");
    }

    [Fact]
    public async Task UpdateBookingAsync_ApprovedBooking_ThrowsInvalidOperation()
    {
        // Arrange
        var booking = CreateTestBooking(EBookingStatus.Approved);
        var dto = new UpdateBookingDto
        {
            Id = booking.Id,
            StartDateTime = DateTime.UtcNow.AddDays(2),
            EndDateTime = DateTime.UtcNow.AddDays(2).AddHours(3)
        };
        _bookingRepoMock.Setup(r => r.GetByIdTrackingWithIncludesAsync(dto.Id, null)).ReturnsAsync(booking);

        // Act
        var act = () => _sut.UpdateBookingAsync(dto, UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only pending or requested bookings can be edited*");
    }

    // ===================== ValidateBookingAsync =====================

    [Fact]
    public async Task ValidateBookingAsync_NoConflicts_ReturnsTrue()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(2);
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
        _maintenanceRepoMock.Setup(r => r.GetScheduledForAircraftInRangeAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<MaintenanceRecord>());
        _availabilityRepoMock.Setup(r => r.HasBlockingAvailabilityAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(false);

        // Act
        var result = await _sut.ValidateBookingAsync(AircraftId, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateBookingAsync_HasMaintenanceBlock_ReturnsFalse()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(2);
        _bookingRepoMock.Setup(r => r.HasOverlappingBookingsAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
        _maintenanceRepoMock.Setup(r => r.GetScheduledForAircraftInRangeAsync(AircraftId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MaintenanceRecord> { new() { AircraftId = AircraftId, Description = new LangStr("Check"), MaintenanceType = new LangStr("Inspection"), PerformedBy = new LangStr("MRO"), StartDate = start, EndDate = end } });

        // Act
        var result = await _sut.ValidateBookingAsync(AircraftId, start, end);

        // Assert
        result.Should().BeFalse();
    }

    // ===================== GetByIdAsync =====================

    [Fact]
    public async Task GetByIdAsync_ExistingBooking_ReturnsDto()
    {
        // Arrange
        var booking = CreateTestBooking();
        _bookingRepoMock.Setup(r => r.GetByIdWithIncludesAsync(booking.Id, CompanyId, UserId)).ReturnsAsync(booking);

        // Act
        var result = await _sut.GetByIdAsync(booking.Id, CompanyId, UserId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(booking.Id);
        result.Status.Should().Be(EBookingStatus.Requested);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingBooking_ReturnsNull()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        _bookingRepoMock.Setup(r => r.GetByIdWithIncludesAsync(bookingId, null, null)).ReturnsAsync((Booking?)null);

        // Act
        var result = await _sut.GetByIdAsync(bookingId);

        // Assert
        result.Should().BeNull();
    }
}