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

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ITenantService _tenantService;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(
        IMaintenanceService maintenanceService,
        ITenantService tenantService,
        ILogger<MaintenanceController> logger)
    {
        _maintenanceService = maintenanceService;
        _tenantService = tenantService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    private async Task<Guid?> ResolveOrAutoSetTenantAsync(Guid userId)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (!tenantId.HasValue)
        {
            var companies = await _tenantService.GetUserCompaniesAsync(userId);
            var first = companies.FirstOrDefault();
            if (first == null) return null;
            _tenantService.SetCurrentTenant(first.CompanyId);
            tenantId = first.CompanyId;
        }
        return tenantId;
    }

    /// <summary>
    /// Get all maintenance records for the current company (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<IEnumerable<MaintenanceRecordResponse>>> GetMaintenanceRecords(
        [FromQuery] Guid? aircraftId = null)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: caller must be a member of the resolved company
        if (!await _tenantService.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        var records = await _maintenanceService.GetAllForCompanyAsync(tenantId.Value, aircraftId);
        return Ok(records.ToResponse());
    }

    /// <summary>
    /// Get a single maintenance record by ID (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<MaintenanceRecordResponse>> GetMaintenanceRecord(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: caller must be a member of the resolved company
        if (!await _tenantService.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        var record = await _maintenanceService.GetByIdAsync(id, tenantId.Value);
        if (record == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Maintenance record with id {id} not found."
            });

        return Ok(record.ToResponse());
    }

    /// <summary>
    /// Create a maintenance record (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<MaintenanceRecordResponse>> PostMaintenanceRecord(
        [FromBody] CreateMaintenanceRecordRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: only company owners can create maintenance records
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var createdBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var record = await _maintenanceService.CreateAsync(request.ToBllDto(), tenantId.Value, createdBy);
            return CreatedAtAction(nameof(GetMaintenanceRecord), new { id = record.Id }, record.ToResponse());
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
    /// Update a maintenance record (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<MaintenanceRecordResponse>> PutMaintenanceRecord(
        Guid id, [FromBody] UpdateMaintenanceRecordRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (id != request.Id)
            return BadRequest();

        var tenantId = await ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: only company owners can update maintenance records
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var updatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var record = await _maintenanceService.UpdateAsync(id, request.ToBllDto(), tenantId.Value, updatedBy);
            return Ok(record.ToResponse());
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
    /// Soft-delete a maintenance record (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<IActionResult> DeleteMaintenanceRecord(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: only company owners can delete maintenance records
        if (!await _tenantService.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var deletedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            await _maintenanceService.DeleteAsync(id, tenantId.Value, deletedBy);
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
