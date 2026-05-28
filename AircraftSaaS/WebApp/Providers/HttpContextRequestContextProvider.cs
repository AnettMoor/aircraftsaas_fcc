using Microsoft.AspNetCore.Http;
using Shared.Contracts.Common;

namespace WebApp.Providers;

/// <summary>
/// ASP.NET Core implementation of IRequestContextProvider using HttpContext.
/// </summary>
public class HttpContextRequestContextProvider : IRequestContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextRequestContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetClientIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetHeaderValue(string headerName)
    {
        return _httpContextAccessor.HttpContext?.Request.Headers[headerName].FirstOrDefault();
    }

    public string? GetCookieValue(string cookieName)
    {
        return _httpContextAccessor.HttpContext?.Request.Cookies[cookieName];
    }

    public void SetCookie(string name, string value, int expiryDays = 30, bool httpOnly = true)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            context.Response.Cookies.Append(name, value, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(expiryDays),
                HttpOnly = httpOnly,
                SameSite = SameSiteMode.Lax
            });
        }
    }

    public string? GetRequestPath()
    {
        return _httpContextAccessor.HttpContext?.Request.Path.Value;
    }
}
