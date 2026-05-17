using App.Domain.Entities;
using Base.Domain;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class AirportTests
{
    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        // Arrange
        var airport = new Airport
        {
            IcaoCode = "EETN",
            IataCode = "TLL",
            Name = new LangStr("Tallinn Airport"),
            Country = new LangStr("Estonia"),
            City = new LangStr("Tallinn")
        };

        // Act
        airport.SoftDelete("admin@test.com");

        // Assert
        airport.IsDeleted.Should().BeTrue();
        airport.DeletedBy.Should().Be("admin@test.com");
        airport.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        // Arrange
        var airport = new Airport
        {
            IcaoCode = "EFHK",
            IataCode = "HEL",
            Name = new LangStr("Helsinki Airport"),
            Country = new LangStr("Finland"),
            City = new LangStr("Helsinki")
        };
        airport.SoftDelete("admin@test.com");

        // Act
        airport.Restore();

        // Assert
        airport.IsDeleted.Should().BeFalse();
        airport.DeletedAt.Should().BeNull();
        airport.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var airport = new Airport
        {
            IcaoCode = "KJFK",
            IataCode = "JFK",
            Name = new LangStr("John F. Kennedy International Airport"),
            Country = new LangStr("United States"),
            City = new LangStr("New York"),
            Latitude = 40.6413,
            Longitude = -73.7781,
            Elevation = 13
        };

        // Assert
        airport.IcaoCode.Should().Be("KJFK");
        airport.IataCode.Should().Be("JFK");
        airport.Latitude.Should().Be(40.6413);
        airport.Longitude.Should().Be(-73.7781);
        airport.Elevation.Should().Be(13);
    }
}
