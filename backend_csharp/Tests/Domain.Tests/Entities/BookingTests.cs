using App.Domain.Entities;
using App.Domain.Enums;
using Base.Domain;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class BookingTests
{
    private Booking CreateBooking(EBookingStatus status = EBookingStatus.Requested) => new()
    {
        AircraftId = Guid.NewGuid(),
        PilotId = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        StartDateTime = DateTime.UtcNow.AddDays(1),
        EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(2),
        Status = status,
        TotalAmount = 300m
    };

    // ── CanApprove ─────────────────────────────────────────────

    [Fact]
    public void CanApprove_StatusIsRequested_ReturnsTrue()
    {
        var booking = CreateBooking(EBookingStatus.Requested);
        booking.CanApprove().Should().BeTrue();
    }

    [Theory]
    [InlineData(EBookingStatus.Pending)]
    [InlineData(EBookingStatus.Approved)]
    [InlineData(EBookingStatus.Paid)]
    [InlineData(EBookingStatus.Completed)]
    [InlineData(EBookingStatus.Cancelled)]
    [InlineData(EBookingStatus.Rejected)]
    public void CanApprove_StatusIsNotRequested_ReturnsFalse(EBookingStatus status)
    {
        var booking = CreateBooking(status);
        booking.CanApprove().Should().BeFalse();
    }

    // ── CanReject ──────────────────────────────────────────────

    [Fact]
    public void CanReject_StatusIsRequested_ReturnsTrue()
    {
        var booking = CreateBooking(EBookingStatus.Requested);
        booking.CanReject().Should().BeTrue();
    }

    [Fact]
    public void CanReject_StatusIsApproved_ReturnsFalse()
    {
        var booking = CreateBooking(EBookingStatus.Approved);
        booking.CanReject().Should().BeFalse();
    }

    // ── CanPay ─────────────────────────────────────────────────

    [Fact]
    public void CanPay_StatusIsApproved_ReturnsTrue()
    {
        var booking = CreateBooking(EBookingStatus.Approved);
        booking.CanPay().Should().BeTrue();
    }

    [Fact]
    public void CanPay_StatusIsPending_ReturnsFalse()
    {
        var booking = CreateBooking(EBookingStatus.Pending);
        booking.CanPay().Should().BeFalse();
    }

    // ── CanCancel ──────────────────────────────────────────────

    [Fact]
    public void CanCancel_StatusIsPending_ReturnsTrue()
    {
        var booking = CreateBooking(EBookingStatus.Pending);
        booking.CanCancel().Should().BeTrue();
    }

    [Fact]
    public void CanCancel_StatusIsCompleted_ReturnsFalse()
    {
        var booking = CreateBooking(EBookingStatus.Completed);
        booking.CanCancel().Should().BeFalse();
    }

    [Fact]
    public void CanCancel_StatusIsCancelled_ReturnsFalse()
    {
        var booking = CreateBooking(EBookingStatus.Cancelled);
        booking.CanCancel().Should().BeFalse();
    }

    // ── CanComplete ────────────────────────────────────────────

    [Fact]
    public void CanComplete_StatusIsPaid_ReturnsTrue()
    {
        var booking = CreateBooking(EBookingStatus.Paid);
        booking.CanComplete().Should().BeTrue();
    }

    [Theory]
    [InlineData(EBookingStatus.Pending)]
    [InlineData(EBookingStatus.Requested)]
    [InlineData(EBookingStatus.Approved)]
    [InlineData(EBookingStatus.Completed)]
    [InlineData(EBookingStatus.Cancelled)]
    public void CanComplete_StatusIsNotPaid_ReturnsFalse(EBookingStatus status)
    {
        var booking = CreateBooking(status);
        booking.CanComplete().Should().BeFalse();
    }

    // ── Soft Delete ────────────────────────────────────────────

    [Fact]
    public void SoftDelete_ValidActor_SetsDeletedFields()
    {
        var booking = CreateBooking();
        booking.SoftDelete("pilot@test.com");

        booking.IsDeleted.Should().BeTrue();
        booking.DeletedBy.Should().Be("pilot@test.com");
        booking.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Restore_PreviouslyDeleted_ClearsDeletedFields()
    {
        var booking = CreateBooking();
        booking.SoftDelete("admin@test.com");

        booking.Restore();

        booking.IsDeleted.Should().BeFalse();
        booking.DeletedAt.Should().BeNull();
        booking.DeletedBy.Should().BeNull();
    }

    // ── AppUserId mapping ──────────────────────────────────────

    [Fact]
    public void AppUserId_MapsToPilotId()
    {
        var pilotId = Guid.NewGuid();
        var booking = CreateBooking();
        booking.PilotId = pilotId;

        booking.AppUserId.Should().Be(pilotId);
    }

    [Fact]
    public void AppUserId_Set_SetsPilotId()
    {
        var booking = CreateBooking();
        var newId = Guid.NewGuid();
        booking.AppUserId = newId;

        booking.PilotId.Should().Be(newId);
    }
}
