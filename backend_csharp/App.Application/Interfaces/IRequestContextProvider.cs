namespace App.Application.Interfaces;

/// <summary>
/// Provides HTTP request context information without depending on HTTP infrastructure.
/// </summary>
public interface IRequestContextProvider
{
    /// <summary>
    /// Gets the client's IP address.
    /// </summary>
    string? GetClientIpAddress();
    
    /// <summary>
    /// Gets a request header value.
    /// </summary>
    string? GetHeaderValue(string headerName);
    
    /// <summary>
    /// Gets a cookie value from the request.
    /// </summary>
    string? GetCookieValue(string cookieName);
    
    /// <summary>
    /// Sets a response cookie with the specified options.
    /// </summary>
    void SetCookie(string name, string value, int expiryDays = 30, bool httpOnly = true);
    
    /// <summary>
    /// Gets the current request path.
    /// </summary>
    string? GetRequestPath();
}
