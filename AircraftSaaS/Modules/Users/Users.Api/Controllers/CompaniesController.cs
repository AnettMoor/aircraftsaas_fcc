using System.Net;
using System.Security.Claims;
using Users.Application.Interfaces;
using Users.Api.DTOs;
using Users.Api.Mappers;
using Asp.Versioning;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Common;
using Shared.Kernel.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Users.Api.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ITenantService _tenantService;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(
        ICompanyService companyService,
        ITenantService tenantService,
        ILogger<CompaniesController> logger)
    {
        _companyService = companyService;
        _tenantService = tenantService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Get all companies (SystemAdmin only).
    /// </summary>
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IEnumerable<CompanyResponse>>> GetCompanies()
    {
        var companies = await _companyService.GetAllAsync();
        return Ok(companies.ToResponse());
    }

    /// <summary>
    /// Get a company by ID. Accessible to SystemAdmin and members of the company.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<CompanyResponse>> GetCompany(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // IDOR ownership check: only SystemAdmin or company members can access a specific company
        var isAdmin = User.IsInRole("SystemAdmin");
        if (!isAdmin)
        {
            var isMember = await _tenantService.IsUserInCompanyAsync(id, userId.Value);
            if (!isMember)
                return Forbid();
        }

        var company = await _companyService.GetByIdAsync(id);
        if (company == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Company with id {id} not found."
            });

        return Ok(company.ToResponse());
    }

    /// <summary>
    /// Get a company by slug (public lookup).
    /// </summary>
    // not used right now (slug based routing isnt used in vue routing)
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<CompanyResponse>> GetCompanyBySlug(string slug)
    {
        var company = await _companyService.GetBySlugAsync(slug);
        if (company == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Company with slug '{slug}' not found."
            });

        return Ok(company.ToResponse());
    }

    /// <summary>
    /// Get the current user's company (tenant-scoped). (Normal, CompanyOwner only — SystemAdmin has no personal company)
    /// </summary>
    [HttpGet("my")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<CompanyResponse>> GetMyCompany()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // Always fetch the user's actual company memberships from the database
        var userCompanies = (await _tenantService.GetUserCompaniesAsync(userId.Value)).ToList();
        if (!userCompanies.Any())
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });
        }

        var tenantId = _tenantService.GetCurrentTenantId();

        // If no tenant header was sent, or the tenant header points to a company
        // the user is no longer a member of (e.g. SystemAdmin reassigned the user),
        // auto-resolve to the user's actual first active company.
        var matchedCompany = tenantId.HasValue
            ? userCompanies.FirstOrDefault(c => c.CompanyId == tenantId.Value)
            : null;

        if (matchedCompany == null)
        {
            matchedCompany = userCompanies.First();
            _tenantService.SetCurrentTenant(matchedCompany.CompanyId);
            tenantId = matchedCompany.CompanyId;
            _logger.LogInformation(
                "Auto-resolved stale tenant context for user {UserId} to company {CompanyId}",
                userId.Value, tenantId);
        }

        var companyDto = await _companyService.GetByIdAsync(matchedCompany.CompanyId);
        if (companyDto == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = "Company not found."
            });

        return Ok(companyDto.ToResponse());
    }

    /// <summary>
    /// Create a new company (SystemAdmin only).
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<CompanyResponse>> PostCompany([FromBody] CreateCompanyRequest request)
    {
        var createdBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        try
        {
            var company = await _companyService.CreateAsync(request.ToBllDto(), createdBy);
            return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, company.ToResponse());
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
    /// Update a company (CompanyOwner of that company only — SystemAdmin uses the admin panel).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<CompanyResponse>> PutCompany(Guid id, [FromBody] UpdateCompanyRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");

        // IDOR ownership check: non-admin users can only update a company they belong to
        if (!isAdmin)
        {
            var userCompanies = await _tenantService.GetUserCompaniesAsync(userId.Value);
            var company = userCompanies.FirstOrDefault(c => c.CompanyId == id);
            if (company == null)
            {
                return Forbid();
            }
        }

        var updatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        try
        {
            // IDOR check is now enforced inside the service; pass callerId + isAdmin
            var company = await _companyService.UpdateAsync(id, request.ToBllDto(), updatedBy, userId.Value, isAdmin);
            return Ok(company.ToResponse());
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = ex.Message
            });
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
    /// Delete a company (SystemAdmin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> DeleteCompany(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");
        var deletedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        try
        {
            // IDOR check is now enforced inside the service; pass callerId + isAdmin
            await _companyService.DeleteAsync(id, deletedBy, userId.Value, isAdmin);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = ex.Message
            });
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
