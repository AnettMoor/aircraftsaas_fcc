using WebApp.v1;
using WebApp.v1.Identity;
using App.Domain.Contracts;
using App.Domain.DTOs;
using Base.Helpers;

namespace WebApp.ApiControllers.Identity;

using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiVersion("1.0")]
[ApiController]
[Route("/api/v{version:apiVersion}/identity/[controller]/[action]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
public class AccountController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _configuration;

    public AccountController(
        IAuthService authService,
        ILogger<AccountController> logger,
        IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _configuration = configuration;
    }


    /// <summary>
    /// Register new local user into app.
    /// </summary>
    /// <param name="registrationData">Username and password. And personal details.</param>
    /// <param name="expiresInSeconds">Override jwt lifetime for testing.</param>
    /// <returns>JWTResponse - jwt and refresh token</returns>
    [AllowAnonymous]
    [HttpPost]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType((int) HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int) HttpStatusCode.BadRequest)]
    public async Task<ActionResult<JWTResponse>> Register(
        [FromBody]
        RegisterInfo registrationData,
        [FromQuery]
        int expiresInSeconds)
    {
        if (expiresInSeconds <= 0) expiresInSeconds = int.MaxValue;
        expiresInSeconds = expiresInSeconds < _configuration.GetValue<int>("JWT:ExpiresInSeconds")
            ? expiresInSeconds
            : _configuration.GetValue<int>("JWT:ExpiresInSeconds");

        var result = await _authService.RegisterAsync(
            registrationData.Email,
            registrationData.Password,
            registrationData.Firstname,
            registrationData.Lastname,
            expiresInSeconds);

        if (!result.Succeeded)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = result.ErrorMessage ?? "Registration failed"
            });
        }

        return Ok(new JWTResponse
        {
            Jwt = result.Jwt!,
            RefreshToken = result.RefreshToken!
        });
    }


    [AllowAnonymous]
    [HttpPost]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType((int) HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int) HttpStatusCode.BadRequest)]
    public async Task<ActionResult<JWTResponse>> Login(
        [FromBody]
        LoginInfo loginInfo,
        [FromQuery]
        int expiresInSeconds
    )
    {
        if (expiresInSeconds <= 0) expiresInSeconds = int.MaxValue;
        expiresInSeconds = expiresInSeconds < _configuration.GetValue<int>("JWT:ExpiresInSeconds")
            ? expiresInSeconds
            : _configuration.GetValue<int>("JWT:ExpiresInSeconds");

        var result = await _authService.LoginAsync(loginInfo.Email, loginInfo.Password, expiresInSeconds);

        if (!result.Succeeded)
        {
            if (result.ErrorType == AuthErrorType.InvalidCredentials)
            {
                return Unauthorized(result.ErrorMessage);
            }

            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = result.ErrorMessage ?? "Login failed"
            });
        }

        return Ok(new JWTResponse
        {
            Jwt = result.Jwt!,
            RefreshToken = result.RefreshToken!
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType((int) HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int) HttpStatusCode.BadRequest)]
    //refresh expired jwt + rotate refhreshtoken
    public async Task<ActionResult<JWTResponse>> RefreshTokenData(
        [FromBody]
        TokenRefreshInfo tokenRefreshInfo,
        [FromQuery]
        int expiresInSeconds
    )
    {
        if (expiresInSeconds <= 0) expiresInSeconds = int.MaxValue;
        expiresInSeconds = expiresInSeconds < _configuration.GetValue<int>("JWT:ExpiresInSeconds")
            ? expiresInSeconds
            : _configuration.GetValue<int>("JWT:ExpiresInSeconds");

        var result = await _authService.RefreshTokenAsync(
            tokenRefreshInfo.Jwt,
            tokenRefreshInfo.RefreshToken,
            expiresInSeconds);

        if (!result.Succeeded)
        {
            return result.ErrorType switch
            {
                AuthErrorType.UserNotFound => NotFound(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.NotFound,
                    Error = result.ErrorMessage ?? "User not found"
                }),
                AuthErrorType.InvalidRefreshToken => NotFound(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.NotFound,
                    Error = result.ErrorMessage ?? "Invalid refresh token"
                }),
                _ => BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = result.ErrorMessage ?? "Token refresh failed"
                })
            };
        }

        return Ok(new JWTResponse
        {
            Jwt = result.Jwt!,
            RefreshToken = result.RefreshToken!
        });
    }

    [HttpPost]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType((int) HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int) HttpStatusCode.BadRequest)]
    public async Task<ActionResult> Logout(
        [FromBody]
        LogoutInfo logout)
    {
        // delete the refresh token - so user is kicked out after jwt expiration
        // We do not invalidate the jwt on serverside - that would require pipeline modification and checking against db on every request
        // so client can actually continue to use the jwt until it expires (keep the jwt expiration time short ~1 min)

        var userId = User.GetUserId();
        if (userId == null)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "Invalid refresh token"
            });
        }

        var result = await _authService.LogoutAsync(userId.Value, logout.RefreshToken);

        if (!result.Succeeded)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = result.ErrorMessage ?? "Logout failed"
            });
        }

        return Ok(new { TokenDeleteCount = result.DeletedTokenCount });
    }
}
