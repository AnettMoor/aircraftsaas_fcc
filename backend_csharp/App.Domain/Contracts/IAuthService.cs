using App.Domain.DTOs;

namespace App.Domain.Contracts;

/// <summary>
/// Domain-layer abstraction for JWT-based authentication operations.
/// Handles user registration, login, token refresh, and logout.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Register a new user and return JWT + refresh token.
    /// </summary>
    Task<JwtAuthResult> RegisterAsync(string email, string password, string firstName, string lastName, int expiresInSeconds);

    /// <summary>
    /// Authenticate a user by email/password and return JWT + refresh token.
    /// </summary>
    Task<JwtAuthResult> LoginAsync(string email, string password, int expiresInSeconds);

    /// <summary>
    /// Refresh an expired JWT using a valid refresh token. Rotates the refresh token.
    /// </summary>
    Task<JwtAuthResult> RefreshTokenAsync(string jwt, string refreshToken, int expiresInSeconds);

    /// <summary>
    /// Logout a user by deleting their refresh tokens.
    /// </summary>
    Task<LogoutResult> LogoutAsync(Guid userId, string refreshToken);
}
