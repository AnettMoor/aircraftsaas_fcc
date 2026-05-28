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
/// Manages insurance policies for a specific aircraft.
/// CompanyOwners can create, update, and delete policies.
/// Any authenticated user can read policies for aircraft they can see.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/aircraft/{aircraftId:guid}/insurance")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class InsurancePoliciesController : ControllerBase
{
    private readonly IInsurancePolicyService _insurancePolicyService;
    private readonly IAircraftService _aircraftService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<InsurancePoliciesController> _logger;

    public InsurancePoliciesController(
        IInsurancePolicyService insurancePolicyService,
        IAircraftService aircraftService,
        ITenantContext tenantContext,
        ILogger<InsurancePoliciesController> logger)
    {
        _insurancePolicyService = insurancePolicyService;
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
    /// Get all insurance policies for an aircraft.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<InsurancePolicyResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<IEnumerable<InsurancePolicyResponse>>> GetPolicies(Guid aircraftId)
    {
        var aircraft = await _aircraftService.GetByIdAsync(aircraftId);
        if (aircraft == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Aircraft with id {aircraftId} not found."
            });

        var policies = await _insurancePolicyService.GetAllForAircraftAsync(aircraftId);
        return Ok(policies.ToResponse());
    }

    /// <summary>
    /// Get a specific insurance policy for an aircraft.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InsurancePolicyResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<InsurancePolicyResponse>> GetPolicy(Guid aircraftId, Guid id)
    {
        var policy = await _insurancePolicyService.GetByIdAsync(id, aircraftId);
        if (policy == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Insurance policy with id {id} not found for aircraft {aircraftId}."
            });

        return Ok(policy.ToResponse());
    }

    /// <summary>
    /// Add a new insurance policy to an aircraft (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType(typeof(InsurancePolicyResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<InsurancePolicyResponse>> PostPolicy(
        Guid aircraftId,
        [FromBody] CreateInsurancePolicyRequest request)
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
            var policy = await _insurancePolicyService.CreateAsync(
                request.ToBllDto(), aircraftId, tenantId.Value);

            return CreatedAtAction(
                nameof(GetPolicy),
                new { aircraftId, id = policy.Id },
                policy.ToResponse());
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
    /// Update an insurance policy (CompanyOwner only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType(typeof(InsurancePolicyResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<InsurancePolicyResponse>> PutPolicy(
        Guid aircraftId,
        Guid id,
        [FromBody] UpdateInsurancePolicyRequest request)
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
            var policy = await _insurancePolicyService.UpdateAsync(
                id, request.ToBllDto(), aircraftId, tenantId.Value);

            return Ok(policy.ToResponse());
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
    /// Soft-delete an insurance policy (CompanyOwner only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> DeletePolicy(Guid aircraftId, Guid id)
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
            await _insurancePolicyService.DeleteAsync(id, aircraftId, tenantId.Value, deletedBy);
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
