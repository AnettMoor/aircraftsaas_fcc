using System.Net;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Api.DTOs.Identity;
using Asp.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Common;
using Shared.Kernel.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Users.Api.Controllers.Identity;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/identity/[controller]/[action]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService authService,
        IConfiguration configuration,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetTokenExpirationSeconds()
    {
        var minutes = _configuration.GetValue<int>("JWT:ExpiresInMinutes");
        return (minutes > 0 ? minutes : 60) * 60;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<JWTResponse>((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<JWTResponse>> Register([FromBody] RegisterInfo model)
    {
        var result = await _authService.RegisterAsync(
            model.Email,
            model.Password,
            model.Firstname,
            model.Lastname,
            GetTokenExpirationSeconds());

        if (!result.Succeeded)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = result.ErrorType switch
                {
                    AuthErrorType.UserAlreadyExists => result.ErrorMessage ?? "User already exists.",
                    AuthErrorType.RegistrationFailed => result.ErrorMessage ?? "Registration failed.",
                    _ => result.ErrorMessage ?? "Registration failed."
                }
            });
        }

        return Ok(new JWTResponse
        {
            Jwt = result.Jwt!,
            RefreshToken = result.RefreshToken!,
        });
    }

    /// <summary>
    /// Log in with email and password.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<JWTResponse>((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<JWTResponse>> Login([FromBody] LoginInfo model)
    {
        var result = await _authService.LoginAsync(
            model.Email,
            model.Password,
            GetTokenExpirationSeconds());

        if (!result.Succeeded)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = result.ErrorType switch
                {
                    AuthErrorType.UserNotFound => "User/password problem.",
                    AuthErrorType.InvalidCredentials => "User/password problem.",
                    _ => result.ErrorMessage ?? "Login failed."
                }
            });
        }

        return Ok(new JWTResponse
        {
            Jwt = result.Jwt!,
            RefreshToken = result.RefreshToken!,
        });
    }

    /// <summary>
    /// Refresh JWT token using a valid refresh token.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<JWTResponse>((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<JWTResponse>> RefreshTokenData([FromBody] TokenRefreshInfo model)
    {
        var result = await _authService.RefreshTokenAsync(
            model.Jwt,
            model.RefreshToken,
            GetTokenExpirationSeconds());

        if (!result.Succeeded)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = result.ErrorType switch
                {
                    AuthErrorType.InvalidToken => "Invalid token.",
                    AuthErrorType.InvalidRefreshToken => "Invalid refresh token.",
                    AuthErrorType.UserNotFound => "User not found.",
                    _ => result.ErrorMessage ?? "Token refresh failed."
                }
            });
        }

        return Ok(new JWTResponse
        {
            Jwt = result.Jwt!,
            RefreshToken = result.RefreshToken!,
        });
    }

    /// <summary>
    /// Log out by revoking the refresh token.
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult> Logout([FromBody] LogoutInfo model)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = "User identity not found."
            });
        }

        var result = await _authService.LogoutAsync(userId.Value, model.RefreshToken);

        if (!result.Succeeded)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = result.ErrorMessage ?? "Logout failed — user or token not found."
            });
        }

        return Ok(new { message = "Logged out successfully." });
    }
}
