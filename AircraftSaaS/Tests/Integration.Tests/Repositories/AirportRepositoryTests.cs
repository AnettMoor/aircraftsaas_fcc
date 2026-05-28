using Fleet.Domain.Entities;
using Fleet.Infrastructure.Repositories;
using Shared.Kernel.Domain;
using FluentAssertions;
using Integration.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Repositories;

public class AirportRepositoryTests : IAsyncLifetime
{
    private readonly TestFleetDbContextFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    /// <summary>
    /// Truncates the Airports table before each test to ensure test isolation.
    /// </summary>
    private async Task CleanupAirportsAsync()
    {
        await using var ctx = _factory.CreateContext();
        ctx.Airports.RemoveRange(ctx.Airports.IgnoreQueryFilters());
        await ctx.SaveChangesAsync();
    }

    private Airport CreateAirport(string icaoCode, string iataCode, string name, string city, string country)
    {
        return new Airport
        {
            IcaoCode = icaoCode,
            IataCode = iataCode,
            Name = new LangStr(name),
            City = new LangStr(city),
            Country = new LangStr(country),
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetByIcaoCodeAsync_ExistingCode_ReturnsAirport()
    {
        // Arrange
        await CleanupAirportsAsync();
        await using var context = _factory.CreateContext();
        context.Airports.Add(CreateAirport("EETN", "TLL", "Tallinn Airport", "Tallinn", "Estonia"));
        await context.SaveChangesAsync();

        await using var queryContext = _factory.CreateContext();
        var repo = new AirportRepository(queryContext);

        // Act
        var result = await repo.GetByIcaoCodeAsync("EETN");

        // Assert
        result.Should().NotBeNull();
        result!.IataCode.Should().Be("TLL");
    }

    [Fact]
    public async Task GetByIcaoCodeAsync_NonExistingCode_ReturnsNull()
    {
        // Arrange
        await CleanupAirportsAsync();
        await using var context = _factory.CreateContext();
        var repo = new AirportRepository(context);

        // Act
        var result = await repo.GetByIcaoCodeAsync("XXXX");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_ByName_ReturnsMatchingAirports()
    {
        // Arrange
        await CleanupAirportsAsync();
        await using var context = _factory.CreateContext();
        context.Airports.AddRange(
            CreateAirport("EETN", "TLL", "Tallinn Airport", "Tallinn", "Estonia"),
            CreateAirport("EFHK", "HEL", "Helsinki-Vantaa Airport", "Helsinki", "Finland"),
            CreateAirport("KJFK", "JFK", "John F. Kennedy International", "New York", "United States")
        );
        await context.SaveChangesAsync();

        await using var queryContext = _factory.CreateContext();
        var repo = new AirportRepository(queryContext);

        // Act
        var results = await repo.SearchAsync("tallinn");

        // Assert
        results.Should().HaveCount(1);
        results.First().IcaoCode.Should().Be("EETN");
    }

    [Fact]
    public async Task SearchAsync_ByIcaoCode_ReturnsMatchingAirports()
    {
        // Arrange
        await CleanupAirportsAsync();
        await using var context = _factory.CreateContext();
        context.Airports.AddRange(
            CreateAirport("EETN", "TLL", "Tallinn Airport", "Tallinn", "Estonia"),
            CreateAirport("EFHK", "HEL", "Helsinki-Vantaa Airport", "Helsinki", "Finland")
        );
        await context.SaveChangesAsync();

        await using var queryContext = _factory.CreateContext();
        var repo = new AirportRepository(queryContext);

        // Act
        var results = await repo.SearchAsync("EFHK");

        // Assert
        results.Should().HaveCount(1);
        results.First().IataCode.Should().Be("HEL");
    }

    [Fact]
    public async Task SearchAsync_NullOrEmpty_ReturnsAllAirports()
    {
        // Arrange
        await CleanupAirportsAsync();
        await using var context = _factory.CreateContext();
        context.Airports.AddRange(
            CreateAirport("EETN", "TLL", "Tallinn Airport", "Tallinn", "Estonia"),
            CreateAirport("EFHK", "HEL", "Helsinki-Vantaa Airport", "Helsinki", "Finland")
        );
        await context.SaveChangesAsync();

        await using var queryContext = _factory.CreateContext();
        var repo = new AirportRepository(queryContext);

        // Act
        var results = await repo.SearchAsync(null);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdTrackingAsync_ExistingId_ReturnsTrackedEntity()
    {
        // Arrange
        await CleanupAirportsAsync();
        await using var context = _factory.CreateContext();
        var airport = CreateAirport("EETN", "TLL", "Tallinn Airport", "Tallinn", "Estonia");
        context.Airports.Add(airport);
        await context.SaveChangesAsync();

        await using var queryContext = _factory.CreateContext();
        var repo = new AirportRepository(queryContext);

        // Act
        var result = await repo.GetByIdTrackingAsync(airport.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(airport.Id);
    }
}
