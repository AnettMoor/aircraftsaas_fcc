using App.Application.DTOs;
using App.Application.Interfaces;
using App.Application.Services;
using App.Domain.Contracts;
using App.Domain.Entities;
using Base.Domain;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class AirportServiceTests
{
    private readonly Mock<IAppUOW> _uowMock;
    private readonly Mock<IAirportRepository> _airportRepoMock;
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
    private readonly AirportService _sut;

    public AirportServiceTests()
    {
        //wire the mocks
        _uowMock = new Mock<IAppUOW>(); //create a proxy that implements IAppUOW without any real db
        _airportRepoMock = new Mock<IAirportRepository>();
        _currentUserProviderMock = new Mock<ICurrentUserProvider>();

        //"when this memeber is called, return this value"
        _uowMock.Setup(u => u.AirportRepository).Returns(_airportRepoMock.Object);
        _currentUserProviderMock.Setup(p => p.GetCurrentUserSubject()).Returns("test-user");

        _sut = new AirportService(_uowMock.Object, _currentUserProviderMock.Object);
    }

    [Fact]
    public async Task CreateAirportAsync_ValidDto_UppercasesCodesAndPersists()
    {
        // Arrange
        var dto = new CreateAirportDto
        {
            IcaoCode = "eetn",
            IataCode = "tll",
            Name = "Tallinn Airport",
            City = "Tallinn",
            Country = "Estonia",
            Latitude = 59.4133,
            Longitude = 24.8328,
            Elevation = 40
        };

        // Act
        var result = await _sut.CreateAirportAsync(dto);

        // Assert
        result.IcaoCode.Should().Be("EETN");
        result.IataCode.Should().Be("TLL");
        result.Name.Should().Be("Tallinn Airport");
        _airportRepoMock.Verify(r => r.Add(It.Is<Airport>(a =>
            a.IcaoCode == "EETN" && a.IataCode == "TLL"
        )), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAirportByIdAsync_ExistingAirport_ReturnsDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var airport = new Airport
        {
            Id = id,
            IcaoCode = "KJFK",
            IataCode = "JFK",
            Name = new LangStr("JFK International"),
            City = new LangStr("New York"),
            Country = new LangStr("US"),
            Latitude = 40.6413,
            Longitude = -73.7781
        };
        _airportRepoMock.Setup(r => r.FindAsync(id, default!, null)).ReturnsAsync(airport);

        // Act
        var result = await _sut.GetAirportByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.IcaoCode.Should().Be("KJFK");
        result.Name.Should().Be("JFK International");
    }

    [Fact]
    public async Task GetAirportByIdAsync_NonExistingAirport_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _airportRepoMock.Setup(r => r.FindAsync(id, default!, null)).ReturnsAsync((Airport?)null);

        // Act
        var result = await _sut.GetAirportByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAirportAsync_NotFound_ThrowsInvalidOperation()
    {
        // Arrange
        var id = Guid.NewGuid();
        _airportRepoMock.Setup(r => r.GetByIdTrackingAsync(id)).ReturnsAsync((Airport?)null);
        var dto = new UpdateAirportDto
        {
            IcaoCode = "EFHK",
            IataCode = "HEL",
            Name = "Helsinki",
            City = "Helsinki",
            Country = "Finland"
        };

        // Act
        var act = () => _sut.UpdateAirportAsync(id, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Airport not found");
    }

    [Fact]
    public async Task UpdateAirportAsync_ValidUpdate_UppercasesCodesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        var airport = new Airport
        {
            Id = id,
            IcaoCode = "EETN",
            IataCode = "TLL",
            Name = new LangStr("Old Name"),
            City = new LangStr("Old City"),
            Country = new LangStr("Old Country")
        };
        _airportRepoMock.Setup(r => r.GetByIdTrackingAsync(id)).ReturnsAsync(airport);
        var dto = new UpdateAirportDto
        {
            IcaoCode = "efhk",
            IataCode = "hel",
            Name = "Helsinki-Vantaa",
            City = "Vantaa",
            Country = "Finland",
            Latitude = 60.3172,
            Longitude = 24.9633,
            Elevation = 54
        };

        // Act
        var result = await _sut.UpdateAirportAsync(id, dto);

        // Assert
        result.IcaoCode.Should().Be("EFHK");
        result.IataCode.Should().Be("HEL");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAirportAsync_ValidAirport_SoftDeletes()
    {
        // Arrange
        var id = Guid.NewGuid();
        var airport = new Airport
        {
            Id = id,
            IcaoCode = "TEST",
            IataCode = "TST",
            Name = new LangStr("Test"),
            City = new LangStr("City"),
            Country = new LangStr("Country")
        };
        _airportRepoMock.Setup(r => r.GetByIdTrackingAsync(id)).ReturnsAsync(airport);

        // Act
        await _sut.DeleteAirportAsync(id);

        // Assert
        airport.IsDeleted.Should().BeTrue();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RestoreAirportAsync_DeletedAirport_RestoresAndReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var airport = new Airport
        {
            Id = id,
            IcaoCode = "EETN",
            IataCode = "TLL",
            Name = new LangStr("Test"),
            City = new LangStr("Test"),
            Country = new LangStr("Test")
        };
        airport.SoftDelete("admin");
        _airportRepoMock.Setup(r => r.GetByIdIgnoreFiltersTrackingAsync(id)).ReturnsAsync(airport);

        // Act
        var result = await _sut.RestoreAirportAsync(id);

        // Assert
        result.Should().BeTrue();
        airport.IsDeleted.Should().BeFalse();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RestoreAirportAsync_NotDeletedAirport_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        var airport = new Airport
        {
            Id = id,
            IcaoCode = "EETN",
            IataCode = "TLL",
            Name = new LangStr("Test"),
            City = new LangStr("Test"),
            Country = new LangStr("Test")
        };
        // Not deleted
        _airportRepoMock.Setup(r => r.GetByIdIgnoreFiltersTrackingAsync(id)).ReturnsAsync(airport);

        // Act
        var result = await _sut.RestoreAirportAsync(id);

        // Assert
        result.Should().BeFalse();
    }
}
