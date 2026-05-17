using System.Net;
using WebApp.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.ApiControllers;

/// <summary>
/// API endpoints for general application features such as language switching.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
public class HomeController : ControllerBase
{
    /// <summary>
    /// Set the preferred UI language by writing a culture cookie.
    /// The cookie persists for 1 year and is read on every subsequent request
    /// by <see cref="CookieRequestCultureProvider"/>.
    /// </summary>
    /// <param name="culture">BCP-47 culture tag, e.g. <c>en</c> or <c>et-EE</c>.</param>
    /// <returns>200 OK with the selected culture name.</returns>
    [AllowAnonymous]
    [HttpPost("set-language")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    
    public IActionResult SetLanguage([FromBody] SetLanguageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Culture))
        {
            return BadRequest(new { error = "Culture must not be empty." });
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(request.Culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            }
        );

        return Ok(new { culture = request.Culture });
    }
}

/// <summary>
/// Request body for <see cref="HomeController.SetLanguage"/>.
/// </summary>
public class SetLanguageRequest
{
    /// <summary>BCP-47 culture tag, e.g. <c>en</c> or <c>et-EE</c>.</summary>
    public string Culture { get; set; } = default!;
}
