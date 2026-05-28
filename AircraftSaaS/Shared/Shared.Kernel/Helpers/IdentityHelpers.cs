using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Kernel.Helpers;

public static class IdentityHelpers
{
    /// <summary>
    /// Returns the current user's ID (parsed as Guid) from their claims, or null if not found.
    /// Usage: User.GetUserId()
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    public static string GenerateJwt(
        IEnumerable<Claim> claims,
        string key,
        string issuer,
        string audience,
        int expiresInSeconds)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: signingCredentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validate JWT token signature and issuer/audience.
    /// Ignores expiration — used during token refresh where the JWT is allowed to be expired.
    /// </summary>
    public static bool ValidateJWT(
        string jwt,
        string key,
        string issuer,
        string audience)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false // allow expired tokens during refresh
        };

        try
        {
            tokenHandler.ValidateToken(jwt, validationParameters, out _);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
