namespace Shared.Contracts.Common;

/// <summary>
/// Provides current user identity information without depending on HTTP infrastructure.
/// </summary>
public interface ICurrentUserProvider
{
    Guid? GetCurrentUserId();
    string? GetCurrentUserSubject();
    string? GetCurrentUserName();
    bool IsAuthenticated();
    string? GetClaimValue(string claimType);
}
