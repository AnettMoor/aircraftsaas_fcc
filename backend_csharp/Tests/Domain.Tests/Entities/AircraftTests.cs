using App.Domain.Entities;
using Base.Domain;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class AircraftTests
{
    private Aircraft CreateAircraft() => new()
    {
        RegistrationNumber = "ES-ABC",
        Make = new LangStr("Cessna"),
        Model = new LangStr("172"),
        Year = 2020,
        Category = new LangStr("SingleEngineLand"),
        TotalAirspeedHours = 500,
        HourlyRate = 150m,
        BaseAirportId = Guid.NewGuid(),
        Description = new LangStr("Test aircraft"),
        IsAvailable = true,
        CompanyId = Guid.NewGuid()
    };

    [Fact]
    public void SoftDelete_ValidActor_SetsDeletedAtAndDeletedBy()
    {
        // Arrange
        var aircraft = CreateAircraft();
        var deletedBy = "admin@test.com";

        // Act
        aircraft.SoftDelete(deletedBy);

        // Assert
        aircraft.DeletedAt.Should().NotBeNull();
        aircraft.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        aircraft.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Restore_PreviouslyDeleted_ClearsDeletedFields()
    {
        // Arrange
        var aircraft = CreateAircraft();
        aircraft.SoftDelete("admin@test.com");

        // Act
        aircraft.Restore();

        // Assert
        aircraft.DeletedAt.Should().BeNull();
        aircraft.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void IsDeleted_WhenDeletedAtHasValue_ReturnsTrue()
    {
        // Arrange
        var aircraft = CreateAircraft();
        aircraft.SoftDelete("admin@test.com");

        // Act & Assert
        aircraft.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void IsDeleted_WhenNotDeleted_ReturnsFalse()
    {
        // Arrange
        var aircraft = CreateAircraft();

        // Act & Assert
        aircraft.IsDeleted.Should().BeFalse();
    }
}
