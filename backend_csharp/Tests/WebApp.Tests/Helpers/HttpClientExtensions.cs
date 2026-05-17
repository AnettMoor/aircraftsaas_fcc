using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApp.Tests.Helpers;

/// <summary>
/// Extension methods for HttpClient to simplify authenticated requests in integration tests.
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Sets the JWT Bearer token on the HttpClient's default request headers.
    /// </summary>
    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clears any authentication header from the HttpClient.
    /// </summary>
    public static void ClearAuth(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Register a new user and return the JWT token.
    /// </summary>
    public static async Task<(string Jwt, string RefreshToken)> RegisterUserAsync(
        this HttpClient client,
        string email,
        string password,
        string firstName = "Test",
        string lastName = "User")
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/Account/Register", new
        {
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName
        });
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JwtResponseDto>(JsonOptions);
        return (content!.Jwt, content.RefreshToken);
    }

    /// <summary>
    /// Login and return the JWT token.
    /// </summary>
    public static async Task<(string Jwt, string RefreshToken)?> LoginAsync(
        this HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/Account/Login", new
        {
            Email = email,
            Password = password
        });

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadFromJsonAsync<JwtResponseDto>(JsonOptions);
        return (content!.Jwt, content.RefreshToken);
    }

    /// <summary>
    /// Login as the seeded SystemAdmin user (1@4 / 4).
    /// Sets the bearer token on the client and returns the JWT.
    /// </summary>
    public static async Task<string> LoginAsAdminAsync(this HttpClient client)
    {
        var result = await client.LoginAsync("1@4", "4");
        if (result == null) throw new InvalidOperationException("Admin login failed. Ensure seeded users are available.");
        client.SetBearerToken(result.Value.Jwt);
        return result.Value.Jwt;
    }

    /// <summary>
    /// Login as the seeded CompanyOwner user (1@2 / 2).
    /// Sets the bearer token on the client and returns the JWT.
    /// </summary>
    public static async Task<string> LoginAsCompanyOwnerAsync(this HttpClient client)
    {
        var result = await client.LoginAsync("1@2", "2");
        if (result == null) throw new InvalidOperationException("CompanyOwner login failed. Ensure seeded users are available.");
        client.SetBearerToken(result.Value.Jwt);
        return result.Value.Jwt;
    }

    /// <summary>
    /// Login as the seeded Normal user (1@3 / 3).
    /// Sets the bearer token on the client and returns the JWT.
    /// </summary>
    public static async Task<string> LoginAsNormalUserAsync(this HttpClient client)
    {
        var result = await client.LoginAsync("1@3", "3");
        if (result == null) throw new InvalidOperationException("Normal user login failed. Ensure seeded users are available.");
        client.SetBearerToken(result.Value.Jwt);
        return result.Value.Jwt;
    }

    /// <summary>
    /// DTO for deserializing JWT responses from the API.
    /// </summary>
    public class JwtResponseDto
    {
        public string Jwt { get; set; } = default!;
        public string RefreshToken { get; set; } = default!;
    }
}
