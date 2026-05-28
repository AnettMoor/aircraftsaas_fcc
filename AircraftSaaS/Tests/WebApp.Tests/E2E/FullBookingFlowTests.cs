using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.E2E;

/// <summary>
/// End-to-end test covering the critical happy-path flow:
/// Login (seeded user) → Access protected endpoints → Admin CRUD operations
/// This validates the full stack integration from HTTP through to the database.
/// </summary>
public class FullBookingFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FullBookingFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullFlow_LoginAndAccessProtectedEndpoints()
    {
        // This test covers the most critical user flow:
        // 1. Login with seeded user (gets JWT)
        // 2. Use JWT to access protected endpoints
        // 3. Verify anonymous access is blocked
        // 4. Admin can perform CRUD operations

        var client = _factory.CreateClient();

        // ── Step 1: Login with seeded Normal user ───────────────────────
        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/Account/Login", new
        {
            Email = "1@3",
            Password = "3"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "login with correct seeded credentials should succeed");

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<JwtResponseDto>(JsonOptions);
        loginResult.Should().NotBeNull();
        loginResult!.Jwt.Should().NotBeNullOrEmpty("should return a valid JWT");
        loginResult.RefreshToken.Should().NotBeNullOrEmpty("should return a refresh token");

        // ── Step 2: Access protected endpoint with JWT ───────────────────
        client.SetBearerToken(loginResult.Jwt);

        // Get my bookings (seeded Normal user may have no bookings, but endpoint returns 200 with empty list).
        // The LINQ translation bug in BookingRepository.GetAllForPilotAsync has been fixed.
        var myBookingsResponse = await client.GetAsync("/api/v1/Bookings/my");
        myBookingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // ── Step 3: Verify anonymous access is blocked ───────────────────
        client.ClearAuth();
        var blockedResponse = await client.GetAsync("/api/v1/Bookings/my");
        blockedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "accessing protected endpoint without JWT should return 401");

        // ── Step 4: Verify public airport endpoint works (authenticated) ─
        var adminClient = _factory.CreateClient();
        await adminClient.LoginAsAdminAsync();

        var airportsResponse = await adminClient.GetAsync("/api/v1/Airports");
        airportsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var airports = await airportsResponse.Content.ReadFromJsonAsync<AirportResponseDto[]>(JsonOptions);
        airports.Should().NotBeNull();
        airports!.Length.Should().BeGreaterOrEqualTo(1, "seeded airports should be available");

        // ── Step 5: Admin can create airport ─────────────────────────────
        var createAirportResponse = await adminClient.PostAsJsonAsync("/api/v1/Airports", new
        {
            IcaoCode = "EEPK",
            IataCode = "PKR",
            Name = "Pärnu Airport",
            City = "Pärnu",
            Country = "Estonia",
            Latitude = 58.372,
            Longitude = 24.472,
            Elevation = 47
        });
        createAirportResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "admin should be able to create airports");

        var newAirport = await createAirportResponse.Content.ReadFromJsonAsync<AirportResponseDto>(JsonOptions);
        newAirport.Should().NotBeNull();
        newAirport!.IcaoCode.Should().Be("EEPK");

        // ── Step 6: Verify the new airport appears in listing ─────────────
        var updatedAirportsResponse = await adminClient.GetAsync("/api/v1/Airports");
        var updatedAirports = await updatedAirportsResponse.Content.ReadFromJsonAsync<AirportResponseDto[]>(JsonOptions);
        updatedAirports.Should().Contain(a => a.IcaoCode == "EEPK",
            "newly created airport should appear in the listing");
    }

    // ── DTOs ─────────────────────────────────────────────────────────────

    private class JwtResponseDto
    {
        public string Jwt { get; set; } = default!;
        public string RefreshToken { get; set; } = default!;
    }

    private class BookingResponseDto
    {
        public Guid Id { get; set; }
        public Guid AircraftId { get; set; }
        public string Status { get; set; } = default!;
        public decimal TotalAmount { get; set; }
    }

    private class AirportResponseDto
    {
        public Guid Id { get; set; }
        public string IcaoCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string City { get; set; } = default!;
    }
}
