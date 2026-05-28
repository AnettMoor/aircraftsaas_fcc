using System.Net;
using System.Security.Claims;
using Fleet.Api.DTOs;
using Fleet.Api.Mappers;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Asp.Versioning;
using Shared.Kernel.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Fleet.Api.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
public class AircraftController : ControllerBase
{
    private readonly IAircraftService _aircraftService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AircraftController> _logger;

    public AircraftController(
        IAircraftService aircraftService,
        ITenantContext tenantContext,
        ILogger<AircraftController> logger)
    {
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
    /// Search/list all aircraft (public catalog).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<AircraftResponse>>> GetAircraft(
        [FromQuery] AircraftSearchRequest? search)
    {
        var searchDto = search?.ToBllDto() ?? new AircraftSearchRequest().ToBllDto();
        var result = await _aircraftService.SearchAsync(searchDto);
        return Ok(result.ToResponse());
    }

    /// <summary>
    /// Get available aircraft for a given time range.
    /// </summary>
    [HttpGet("available")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<IEnumerable<AircraftResponse>>> GetAvailableAircraft(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] string? location = null)
    {
        if (start >= end)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "Start date must be before end date."
            });

        var result = await _aircraftService.GetAvailableAsync(start, end, location);
        return Ok(result.ToResponse());
    }

    /// <summary>
    /// Get a single aircraft by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<AircraftResponse>> GetAircraft(Guid id)
    {
        var aircraft = await _aircraftService.GetByIdAsync(id);
        if (aircraft == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Aircraft with id {id} not found."
            });

        return Ok(aircraft.ToResponse());
    }

    /// <summary>
    /// Get all aircraft for the current user's company (tenant-scoped).
    /// </summary>
    [HttpGet("company")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<IEnumerable<AircraftResponse>>> GetCompanyAircraft()
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

        if (!await _tenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        var aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
        return Ok(aircraft.ToResponse());
    }

    /// <summary>
    /// Get all soft-deleted (deactivated) aircraft for the current user's company (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpGet("company/deleted")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<ActionResult<IEnumerable<AircraftResponse>>> GetCompanyDeletedAircraft()
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

        var aircraft = await _aircraftService.GetAllDeletedAsync(tenantId.Value);
        return Ok(aircraft.ToResponse());
    }

    /// <summary>
    /// Create a new aircraft (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<ActionResult<AircraftResponse>> PostAircraft([FromBody] CreateAircraftRequest request)
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
            var createdBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var aircraft = await _aircraftService.CreateAsync(request.ToBllDto(), tenantId.Value, createdBy);
            return CreatedAtAction(nameof(GetAircraft), new { id = aircraft.Id }, aircraft.ToResponse());
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
    /// Update an aircraft (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<ActionResult<AircraftResponse>> PutAircraft(Guid id, [FromBody] UpdateAircraftRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (id != request.Id)
        {
            return BadRequest();
        }

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
            var updatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var aircraft = await _aircraftService.UpdateAsync(id, request.ToBllDto(), tenantId.Value, updatedBy);
            return Ok(aircraft.ToResponse());
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
    /// Soft-delete an aircraft (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> DeleteAircraft(Guid id)
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
            await _aircraftService.DeleteAsync(id, tenantId.Value, deletedBy);
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

    /// <summary>
    /// Restore a soft-deleted aircraft (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> RestoreAircraft(Guid id)
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
            var restoredBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            await _aircraftService.RestoreAsync(id, tenantId.Value, restoredBy);
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
