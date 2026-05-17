using System.Net;
using System.Security.Claims;
using App.Application.Interfaces;
using WebApp.v1;
using WebApp.v1.Mappers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.ApiControllers;

/// <summary>
/// Manages pilot licenses. Each authenticated user can only manage their own licenses.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/licenses")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,  Roles = "Normal")]
public class LicensesController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ILogger<LicensesController> _logger;

    public LicensesController(
        ILicenseService licenseService,
        ILogger<LicensesController> logger)
    {
        _licenseService = licenseService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Get all licenses for the current pilot.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LicenseResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IEnumerable<LicenseResponse>>> GetLicenses()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var licenses = await _licenseService.GetAllForUserAsync(userId.Value);
        return Ok(licenses.ToResponse());
    }

    /// <summary>
    /// Get a specific license for the current pilot.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LicenseResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<LicenseResponse>> GetLicense(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

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
    /// Add a new license for the current pilot.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LicenseResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<LicenseResponse>> PostLicense(
        [FromBody] CreateLicenseRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        try
        {
            var license = await _licenseService.CreateAsync(request.ToBllDto(), userId.Value);

            return CreatedAtAction(
                nameof(GetLicense),
                new { id = license.Id },
                license.ToResponse());
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
    /// Update an existing license for the current pilot.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LicenseResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<LicenseResponse>> PutLicense(
        Guid id,
        [FromBody] UpdateLicenseRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (id != request.Id)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "Route id does not match request body id."
            });

        try
        {
            var license = await _licenseService.UpdateAsync(id, request.ToBllDto(), userId.Value);
            return Ok(license.ToResponse());
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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
    /// Soft-delete a license for the current pilot.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> DeleteLicense(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        try
        {
            var deletedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            await _licenseService.DeleteAsync(id, userId.Value, deletedBy);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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
