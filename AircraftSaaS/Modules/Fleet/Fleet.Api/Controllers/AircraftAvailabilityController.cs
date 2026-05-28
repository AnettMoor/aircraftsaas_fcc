using System.Net;
using System.Security.Claims;
using Fleet.Api.DTOs;
using Fleet.Api.Mappers;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Fleet.Api.Controllers;

/// <summary>
/// Manages availability windows for a specific aircraft.
/// CompanyOwners can create, update, and delete availability blocks.
/// Anyone can read availability records for public aircraft listings.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/aircraft/{aircraftId:guid}/availability")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AircraftAvailabilityController : ControllerBase
{
    private readonly IAircraftAvailabilityService _availabilityService;
    private readonly IAircraftService _aircraftService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AircraftAvailabilityController> _logger;

    public AircraftAvailabilityController(
        IAircraftAvailabilityService availabilityService,
        IAircraftService aircraftService,
        ITenantContext tenantContext,
        ILogger<AircraftAvailabilityController> logger)
    {
        _availabilityService = availabilityService;
        _aircraftService = aircraftService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Get all availability records for an aircraft.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<AircraftAvailabilityResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<IEnumerable<AircraftAvailabilityResponse>>> GetAvailabilities(Guid aircraftId)
    {
        var aircraft = await _aircraftService.GetByIdAsync(aircraftId);
        if (aircraft == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Aircraft with id {aircraftId} not found."
            });

        var availabilities = await _availabilityService.GetAllForAircraftAsync(aircraftId);
        return Ok(availabilities.ToResponse());
    }

    /// <summary>
    /// Get a specific availability record for an aircraft.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AircraftAvailabilityResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<AircraftAvailabilityResponse>> GetAvailability(Guid aircraftId, Guid id)
    {
        var availability = await _availabilityService.GetByIdAsync(id, aircraftId);
        if (availability == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Availability record with id {id} not found for aircraft {aircraftId}."
            });

        return Ok(availability.ToResponse());
    }

    /// <summary>
    /// Add a new availability block to an aircraft (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType(typeof(AircraftAvailabilityResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<AircraftAvailabilityResponse>> PostAvailability(
        Guid aircraftId,
        [FromBody] CreateAircraftAvailabilityRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        if (!await _tenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var availability = await _availabilityService.CreateAsync(
                request.ToBllDto(), aircraftId, tenantId.Value);

            return CreatedAtAction(
                nameof(GetAvailability),
                new { aircraftId, id = availability.Id },
                availability.ToResponse());
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
    /// Update an availability record (CompanyOwner only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType(typeof(AircraftAvailabilityResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<AircraftAvailabilityResponse>> PutAvailability(
        Guid aircraftId,
        Guid id,
        [FromBody] UpdateAircraftAvailabilityRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (id != request.Id)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "Route id does not match request body id."
            });

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        if (!await _tenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var availability = await _availabilityService.UpdateAsync(
                id, request.ToBllDto(), aircraftId, tenantId.Value);

            return Ok(availability.ToResponse());
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
    /// Soft-delete an availability record (CompanyOwner only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> DeleteAvailability(Guid aircraftId, Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        if (!await _tenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var deletedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            await _availabilityService.DeleteAsync(id, aircraftId, tenantId.Value, deletedBy);
            return NoContent();
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
}
