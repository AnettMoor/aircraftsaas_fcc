using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Auth;

public class AuthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Register a new user and verify it succeeds.
    /// (Previously failed with 500 due to AppUserCompany.CreatedBy NOT NULL constraint — now fixed.)
    /// </summary>
    [Fact]
    public async Task Register_ValidPayload_ReturnsOk()
    {
        // Arrange
        var email = $"register-test-{Guid.NewGuid():N}@test.com";

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/identity/Account/Register", new
        {
            Email = email,
            Password = "TestPass123",
            FirstName = "Test",
            LastName = "User"
        });

        // Assert — Registration should succeed now that CreatedBy is set properly.
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register endpoint should succeed for valid payload");
    }

    [Fact]
    public async Task Login_ValidSeededCredentials_ReturnsJwt()
    {
        // Arrange — use seeded Normal user (1@3 / 3)
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/identity/Account/Login", new
        {
            Email = "1@3",
            Password = "3"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<HttpClientExtensions.JwtResponseDto>();
        content.Should().NotBeNull();
        content!.Jwt.Should().NotBeNullOrEmpty();
        content.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorizedOrBadRequest()
    {
        // Arrange & Act
        var response = await _client.PostAsJsonAsync("/api/v1/identity/Account/Login", new
        {
            Email = "nonexistent@test.com",
            Password = "WrongPassword"
        });

        // Assert — 404 (user not found) or 400 (bad request)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Arrange
        _client.ClearAuth();

        // Act — /api/v1/Bookings/my requires authentication (not AllowAnonymous)
        var response = await _client.GetAsync("/api/v1/Bookings/my");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        // Arrange — login as seeded Normal user
        await _client.LoginAsNormalUserAsync();

        // Act — /api/v1/Bookings/my requires authentication and returns the pilot's bookings.
        // The LINQ translation bug in BookingRepository.GetAllForPilotAsync has been fixed.
        var response = await _client.GetAsync("/api/v1/Bookings/my");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_SeededCompanyOwner_ReturnsJwt()
    {
        // Arrange & Act — use seeded CompanyOwner user (1@2 / 2)
        var response = await _client.PostAsJsonAsync("/api/v1/identity/Account/Login", new
        {
            Email = "1@2",
            Password = "2"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<HttpClientExtensions.JwtResponseDto>();
        content.Should().NotBeNull();
        content!.Jwt.Should().NotBeNullOrEmpty();
    }
}
