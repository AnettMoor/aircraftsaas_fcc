using System.Net;
using Users.Application.Interfaces;
using Users.Api.DTOs;
using Users.Api.Mappers;
using Asp.Versioning;
using Shared.Contracts.Common;
using Shared.Kernel.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Users.Api.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LicensesController : ControllerBase
{
    private readonly ILicenseService _licenseService;

    public LicensesController(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    /// <summary>
    /// Get all licenses for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IEnumerable<LicenseResponse>>> GetLicenses()
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = "User identity not found."
            });

        var licenses = await _licenseService.GetAllForUserAsync(userId.Value);
        return Ok(licenses.ToResponse());
    }

    /// <summary>
    /// Get a license by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<LicenseResponse>> GetLicense(Guid id)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = "User identity not found."
            });

        var license = await _licenseService.GetByIdAsync(id, userId.Value);
        if (license == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"License with id {id} not found."
            });

        return Ok(license.ToResponse());
    }

    /// <summary>
    /// Create a new license.
    /// </summary>
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<LicenseResponse>> PostLicense([FromBody] CreateLicenseRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = "User identity not found."
            });

        try
        {
            var license = await _licenseService.CreateAsync(request.ToBllDto(), userId.Value);
            return CreatedAtAction(nameof(GetLicense), new { id = license.Id }, license.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Update an existing license.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<LicenseResponse>> PutLicense(Guid id, [FromBody] UpdateLicenseRequest request)
    {
        if (id != request.Id)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "URL id does not match request body id."
            });

        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = "User identity not found."
            });

        try
        {
            var license = await _licenseService.UpdateAsync(id, request.ToBllDto(), userId.Value);
            return Ok(license.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Delete a license.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteLicense(Guid id)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = "User identity not found."
            });

        var userEmail = User.Identity?.Name ?? "unknown";

        try
        {
            await _licenseService.DeleteAsync(id, userId.Value, userEmail);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = ex.Message
            });
        }
    }
}
