using System.Net;
using System.Security.Claims;
using App.Application.Interfaces;
using WebApp.v1;
using WebApp.v1.Mappers;
using Asp.Versioning;
using Base.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.ApiControllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
public class AircraftController : ControllerBase
{
    private readonly IAircraftService _aircraftService;
    private readonly ITenantService _tenantService;  //resolves current tenant (company) from headers or user context
    private readonly ILogger<AircraftController> _logger;

    public AircraftController(
        IAircraftService aircraftService,
        ITenantService tenantService,
        ILogger<AircraftController> logger)
    {
        _aircraftService = aircraftService;
        _tenantService = tenantService;
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
    [HttpGet] // responds to GET api/v1/aircraft.
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<AircraftResponse>>> GetAircraft( //Returns a list of AircraftResponse objects wrapped in an ActionResult.
        [FromQuery] AircraftSearchRequest? search)
    {
        //convert public api dto into bll dto
        var searchDto = search?.ToBllDto() ?? new AircraftSearchRequest().ToBllDto();
        //build query against database
        var result = await _aircraftService.SearchAsync(searchDto);
        //map bll result back into api response dto and wrap in 200ok
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

        // Delegates to the service to find aircraft with no conflicting bookings in that time range.
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
        //get user id from jwt, if missing 401 unauthorized
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            //if no id, try to read id set by middleware (nt headerist)
            var companies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var first = companies.FirstOrDefault();
            if (first == null)
                return BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = "No company context found."
                });
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }

        // IDOR ownership check: verify the caller actually belongs to the resolved company
        if (!await _tenantService.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        // fetch all aircraft scoped to the company
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

        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var companies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var first = companies.FirstOrDefault();
            if (first == null)
                return BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = "No company context found."
                });
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }

        // IDOR ownership check: only company owners can see deleted aircraft
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
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

        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var companies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var first = companies.FirstOrDefault();
            if (first == null)
                return BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = "No company context found."
                });
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }

        // IDOR ownership check: only company owners can create aircraft for their company
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
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

        //id = from url
        //does the id in url match the request id in json body
        if (id != request.Id)
        {
            return BadRequest();
        }

        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var companies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var first = companies.FirstOrDefault();
            if (first == null)
                return BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = "No company context found."
                });
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }

        // IDOR ownership check: only company owners can update aircraft in their company
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
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

        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var companies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var first = companies.FirstOrDefault();
            if (first == null)
                return BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = "No company context found."
                });
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }

        // IDOR ownership check: only company owners can delete aircraft in their company
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
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
    [HttpPost("{id:guid}/restore")] //url-ist võetakse id ja pannakse kohe actioni külge
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> RestoreAircraft(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var companies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var first = companies.FirstOrDefault();
            if (first == null)
                return BadRequest(new RestApiErrorResponse
                {
                    Status = HttpStatusCode.BadRequest,
                    Error = "No company context found."
                });
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }

        // IDOR ownership check: only company owners can restore aircraft in their company
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
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
