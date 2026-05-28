using Booking.Domain.Entities;
using FluentAssertions;

namespace Booking.Tests.Entities;

public class PaymentTests
{
    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        // Arrange
        var payment = new Payment
        {
            BookingId = Guid.NewGuid(),
            PaymentMethod = "CreditCard",
            Amount = 500m,
            Status = "Pending"
        };

        // Act
        payment.SoftDelete("admin@test.com");

        // Assert
        payment.IsDeleted.Should().BeTrue();
        payment.DeletedBy.Should().Be("admin@test.com");
        payment.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        // Arrange
        var payment = new Payment
        {
            BookingId = Guid.NewGuid(),
            PaymentMethod = "BankTransfer",
            Amount = 250m,
            Status = "Completed"
        };
        payment.SoftDelete("admin@test.com");

        // Act
        payment.Restore();

        // Assert
        payment.IsDeleted.Should().BeFalse();
        payment.DeletedAt.Should().BeNull();
        payment.DeletedBy.Should().BeNull();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Refunded")]
    public void Status_CanBeSetToVariousValues(string status)
    {
        // Arrange & Act
        var payment = new Payment
        {
            BookingId = Guid.NewGuid(),
            PaymentMethod = "CreditCard",
            Amount = 100m,
            Status = status
        };

        // Assert
        payment.Status.Should().Be(status);
    }
}
