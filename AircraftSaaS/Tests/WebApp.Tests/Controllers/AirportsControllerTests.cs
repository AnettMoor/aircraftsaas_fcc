using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Controllers;

public class AirportsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AirportsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET (public, anonymous) ─────────────────────────────────────────

    [Fact]
    public async Task GetAirports_Anonymous_ReturnsSeededAirports()
    {
        // Arrange
        _client.ClearAuth();

        // Act
        var response = await _client.GetAsync("/api/v1/Airports");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var airports = await response.Content.ReadFromJsonAsync<AirportResponseDto[]>(JsonOptions);
        airports.Should().NotBeNull();
        airports!.Length.Should().BeGreaterOrEqualTo(3, "seeded data includes 3 airports");
    }

    [Fact]
    public async Task GetAirport_ExistingId_ReturnsAirport()
    {
        // Arrange — fetch all airports first to get a valid ID
        _client.ClearAuth();
        var allResponse = await _client.GetAsync("/api/v1/Airports");
        var airports = await allResponse.Content.ReadFromJsonAsync<AirportResponseDto[]>(JsonOptions);
        var firstAirport = airports!.First();

        // Act
        var response = await _client.GetAsync($"/api/v1/Airports/{firstAirport.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var airport = await response.Content.ReadFromJsonAsync<AirportResponseDto>(JsonOptions);
        airport.Should().NotBeNull();
        airport!.Id.Should().Be(firstAirport.Id);
        airport.IcaoCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAirport_NonExistingId_Returns404()
    {
        // Arrange
        _client.ClearAuth();
        var fakeId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/Airports/{fakeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchAirports_ByName_ReturnsMatchingResults()
    {
        // Arrange
        _client.ClearAuth();

        // Act — search for "Tallinn" which is in the seeded data
        var response = await _client.GetAsync("/api/v1/Airports/search?term=Tallinn");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var airports = await response.Content.ReadFromJsonAsync<AirportResponseDto[]>(JsonOptions);
        airports.Should().NotBeNull();
        airports!.Should().Contain(a => a.City.Contains("Tallinn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAirportByIcao_ExistingCode_ReturnsAirport()
    {
        // Arrange
        _client.ClearAuth();

        // Act — TLLA is a seeded airport ICAO code
        var response = await _client.GetAsync("/api/v1/Airports/icao/TLLA");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var airport = await response.Content.ReadFromJsonAsync<AirportResponseDto>(JsonOptions);
        airport.Should().NotBeNull();
        airport!.IcaoCode.Should().Be("TLLA");
    }

    [Fact]
    public async Task GetAirportByIcao_NonExistingCode_Returns404()
    {
        // Arrange
        _client.ClearAuth();

        // Act
        var response = await _client.GetAsync("/api/v1/Airports/icao/XXXX");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST / PUT / DELETE (SystemAdmin only) ──────────────────────────

    [Fact]
    public async Task PostAirport_AsAdmin_ReturnsCreated()
    {
        // Arrange
        await _client.LoginAsAdminAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Airports", new
        {
            IcaoCode = "EEKE",
            IataCode = "KDL",
            Name = "Kuressaare Airport",
            City = "Kuressaare",
            Country = "Estonia",
            Latitude = 58.2298,
            Longitude = 22.5098,
            Elevation = 14
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var airport = await response.Content.ReadFromJsonAsync<AirportResponseDto>(JsonOptions);
        airport.Should().NotBeNull();
        airport!.IcaoCode.Should().Be("EEKE");
        airport.Name.Should().Contain("Kuressaare");
    }

    [Fact]
    public async Task PostAirport_AsNormalUser_ReturnsForbidden()
    {
        // Arrange — Normal users should NOT be able to create airports
        await _client.LoginAsNormalUserAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Airports", new
        {
            IcaoCode = "XXXX",
            IataCode = "XXX",
            Name = "Unauthorized Airport",
            City = "Nowhere",
            Country = "Nowhere",
            Latitude = 0.0,
            Longitude = 0.0,
            Elevation = 0
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAirport_AsAdmin_ReturnsNoContent()
    {
        // Arrange — create an airport first, then delete it
        await _client.LoginAsAdminAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/Airports", new
        {
            IcaoCode = "EEDL",
            IataCode = "DEL",
            Name = "Deleteable Airport",
            City = "ToDelete",
            Country = "Estonia",
            Latitude = 59.0,
            Longitude = 24.0,
            Elevation = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<AirportResponseDto>(JsonOptions);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/Airports/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAirport_AsNormalUser_ReturnsForbidden()
    {
        // Arrange
        await _client.LoginAsNormalUserAsync();

        // Act — try to delete a seeded airport
        _client.ClearAuth();
        var allResponse = await _client.GetAsync("/api/v1/Airports");
        var airports = await allResponse.Content.ReadFromJsonAsync<AirportResponseDto[]>(JsonOptions);
        var target = airports!.First();

        await _client.LoginAsNormalUserAsync();
        var response = await _client.DeleteAsync($"/api/v1/Airports/{target.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DTO for deserialization ─────────────────────────────────────────

    private class AirportResponseDto
    {
        public Guid Id { get; set; }
        public string IcaoCode { get; set; } = default!;
        public string IataCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string City { get; set; } = default!;
        public string Country { get; set; } = default!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Elevation { get; set; }
    }
}
