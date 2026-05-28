using Fleet.Domain.Entities;
using FluentAssertions;

namespace Fleet.Tests.Entities;

public class AircraftAvailabilityTests
{
    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        // Arrange
        var availability = new AircraftAvailability
        {
            AircraftId = Guid.NewGuid(),
            StartDateTime = DateTime.UtcNow,
            EndDateTime = DateTime.UtcNow.AddHours(4),
            AvailabilityType = "Available"
        };

        // Act
        availability.SoftDelete("owner@test.com");

        // Assert
        availability.IsDeleted.Should().BeTrue();
        availability.DeletedBy.Should().Be("owner@test.com");
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        // Arrange
        var availability = new AircraftAvailability
        {
            AircraftId = Guid.NewGuid(),
            StartDateTime = DateTime.UtcNow,
            EndDateTime = DateTime.UtcNow.AddHours(8),
            AvailabilityType = "Blocked"
        };
        availability.SoftDelete("owner@test.com");

        // Act
        availability.Restore();

        // Assert
        availability.IsDeleted.Should().BeFalse();
        availability.DeletedAt.Should().BeNull();
        availability.DeletedBy.Should().BeNull();
    }

    [Theory]
    [InlineData("Available")]
    [InlineData("Maintenance")]
    [InlineData("Blocked")]
    [InlineData("Booked")]
    public void AvailabilityType_CanBeSetToVariousValues(string availabilityType)
    {
        // Arrange & Act
        var availability = new AircraftAvailability
        {
            AircraftId = Guid.NewGuid(),
            StartDateTime = DateTime.UtcNow,
            EndDateTime = DateTime.UtcNow.AddDays(1),
            AvailabilityType = availabilityType
        };

        // Assert
        availability.AvailabilityType.Should().Be(availabilityType);
    }
}
