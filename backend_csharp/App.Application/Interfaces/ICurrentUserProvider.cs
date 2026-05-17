namespace App.Application.Interfaces;

/// <summary>
/// Provides current user identity information without depending on HTTP infrastructure.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>
    /// Gets the current authenticated user's ID (from NameIdentifier claim).
    /// </summary>
    Guid? GetCurrentUserId();
    
    /// <summary>
    /// Gets the current user's subject identifier (from "sub" claim).
    /// </summary>
    string? GetCurrentUserSubject();
    
    /// <summary>
    /// Gets the current user's display name.
    /// </summary>
    string? GetCurrentUserName();
    
    /// <summary>
    /// Returns whether the current user is authenticated.
    /// </summary>
    bool IsAuthenticated();
    
    /// <summary>
    /// Gets a specific claim value for the current user.
    /// </summary>
    string? GetClaimValue(string claimType);
}
