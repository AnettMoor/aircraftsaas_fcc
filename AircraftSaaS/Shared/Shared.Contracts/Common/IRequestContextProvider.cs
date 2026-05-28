namespace Shared.Contracts.Common;

/// <summary>
/// Provides HTTP request context information without depending on HTTP infrastructure.
/// </summary>
public interface IRequestContextProvider
{
    string? GetClientIpAddress();
    string? GetHeaderValue(string headerName);
    string? GetCookieValue(string cookieName);
    void SetCookie(string name, string value, int expiryDays = 30, bool httpOnly = true);
    string? GetRequestPath();
}
