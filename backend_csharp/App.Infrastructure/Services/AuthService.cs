using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Domain.Contracts;
using App.Domain.DTOs;
using App.Domain.Identity;
using Base.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IAuthService"/>.
/// Lives in Infrastructure because it depends on ASP.NET Identity (UserManager, SignInManager)
/// and IConfiguration for JWT settings.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IConfiguration configuration,
        IRefreshTokenRepository refreshTokenRepo,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _refreshTokenRepo = refreshTokenRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JwtAuthResult> RegisterAsync(
        string email, string password, string firstName, string lastName, int expiresInSeconds)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            _logger.LogWarning("User with email {Email} is already registered", email);
            return JwtAuthResult.Fail(AuthErrorType.UserAlreadyExists,
                $"User with email {email} is already registered");
        }

        // Create user with a refresh token
        var refreshToken = new AppRefreshToken();
        var appUser = new AppUser
        {
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            RefreshTokens = new List<AppRefreshToken> { refreshToken }
        };
        refreshToken.AppUser = appUser;

        var result = await _userManager.CreateAsync(appUser, password);
        if (!result.Succeeded)
        {
            return JwtAuthResult.Fail(AuthErrorType.RegistrationFailed,
                result.Errors.First().Description);
        }

        // Add the Normal Identity role
        var roleResult = await _userManager.AddToRoleAsync(appUser, "Normal");
        if (!roleResult.Succeeded)
        {
            return JwtAuthResult.Fail(AuthErrorType.RegistrationFailed,
                roleResult.Errors.First().Description);
        }

        // Re-fetch user to get generated data
        appUser = await _userManager.FindByEmailAsync(email);
        if (appUser == null)
        {
            _logger.LogWarning("User with email {Email} not found after registration", email);
            return JwtAuthResult.Fail(AuthErrorType.UserNotFound,
                $"User with email {email} is not found after registration");
        }

        var jwt = await GenerateJwtAsync(appUser, expiresInSeconds);
        return JwtAuthResult.Success(jwt, refreshToken.RefreshToken);
    }

    /// <inheritdoc />
    public async Task<JwtAuthResult> LoginAsync(string email, string password, int expiresInSeconds)
    {
        var appUser = await _userManager.FindByEmailAsync(email);
        if (appUser == null)
        {
            _logger.LogWarning("WebApi login failed, email {Email} not found", email);
            return JwtAuthResult.Fail(AuthErrorType.InvalidCredentials, "User/Password problem");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(appUser, password, false);
        if (!result.Succeeded)
        {
            _logger.LogWarning("WebApi login failed, password for email {Email} was wrong", email);
            return JwtAuthResult.Fail(AuthErrorType.InvalidCredentials, "User/Password problem");
        }

        // Clean up expired refresh tokens
        await _refreshTokenRepo.DeleteExpiredTokensAsync(appUser.Id);

        // Create new refresh token
        var refreshToken = new AppRefreshToken { AppUserId = appUser.Id };
        await _refreshTokenRepo.CreateAsync(refreshToken);

        var jwt = await GenerateJwtAsync(appUser, expiresInSeconds);
        return JwtAuthResult.Success(jwt, refreshToken.RefreshToken);
    }

    // generate new jwt
    public async Task<JwtAuthResult> RefreshTokenAsync(string jwt, string refreshToken, int expiresInSeconds)
    {
        // Parse JWT
        JwtSecurityToken? jwtToken;
        try
        {
            jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
            if (jwtToken == null)
            {
                return JwtAuthResult.Fail(AuthErrorType.InvalidToken, "No token");
            }
        }
        catch (Exception)
        {
            return JwtAuthResult.Fail(AuthErrorType.InvalidToken, "No token");
        }

        // Validate JWT signature (ignoring expiration)
        if (!IdentityHelpers.ValidateJWT(
                jwt,
                _configuration.GetValue<string>("JWT:Key")!,
                _configuration.GetValue<string>("JWT:Issuer")!,
                _configuration.GetValue<string>("JWT:Audience")!))
        {
            return JwtAuthResult.Fail(AuthErrorType.InvalidToken, "JWT validation fail");
        }

        var userEmail = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
        if (userEmail == null)
        {
            return JwtAuthResult.Fail(AuthErrorType.InvalidToken, "No email in jwt");
        }

        var appUser = await _userManager.FindByEmailAsync(userEmail);
        if (appUser == null)
        {
            return JwtAuthResult.Fail(AuthErrorType.UserNotFound, $"User with email {userEmail} not found");
        }

        // Load valid refresh tokens
        var validTokens = await _refreshTokenRepo.GetValidTokensForUserAsync(appUser.Id, refreshToken);

        if (validTokens.Count == 0)
        {
            return JwtAuthResult.Fail(AuthErrorType.InvalidRefreshToken,
                "RefreshTokens collection is null or empty - 0");
        }

        if (validTokens.Count != 1)
        {
            return JwtAuthResult.Fail(AuthErrorType.InvalidRefreshToken,
                "More than one valid refresh token found");
        }

        // Generate new JWT
        var newJwt = await GenerateJwtAsync(appUser, expiresInSeconds);

        // Rotate refresh token, keep old one valid for a short period
        var existingToken = validTokens.First();
        if (existingToken.RefreshToken == refreshToken)
        {
            existingToken.PreviousRefreshToken = existingToken.RefreshToken;
            existingToken.PreviousExpirationDT = DateTime.UtcNow.AddMinutes(1);

            existingToken.RefreshToken = Guid.NewGuid().ToString();
            existingToken.ExpirationDT = DateTime.UtcNow.AddDays(7);

            await _refreshTokenRepo.SaveChangesAsync();
        }

        return JwtAuthResult.Success(newJwt, existingToken.RefreshToken);
    }

    /// <inheritdoc />
    public async Task<LogoutResult> LogoutAsync(Guid userId, string refreshToken)
    {
        // Load matching refresh tokens
        var tokens = await _refreshTokenRepo.GetTokensByValueAsync(userId, refreshToken);

        foreach (var token in tokens)
        {
            _refreshTokenRepo.Remove(token);
        }

        var deleteCount = await _refreshTokenRepo.SaveChangesAsync();
        return LogoutResult.Success(deleteCount);
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    private async Task<string> GenerateJwtAsync(AppUser appUser, int expiresInSeconds)
    {
        var claimsPrincipal = await _signInManager.CreateUserPrincipalAsync(appUser);
        return IdentityHelpers.GenerateJwt(
            claimsPrincipal.Claims,
            _configuration.GetValue<string>("JWT:Key")!,
            _configuration.GetValue<string>("JWT:Issuer")!,
            _configuration.GetValue<string>("JWT:Audience")!,
            expiresInSeconds);
    }
}
