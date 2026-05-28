using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Common;
using Users.Application.DTOs;
using Users.Application.Interfaces;

namespace Users.WebHost.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/admin")]
[ApiController]
public class InternalAdminController : ControllerBase
{
    private readonly ISystemAdminUsersService _adminService;

    public InternalAdminController(ISystemAdminUsersService adminService)
    {
        _adminService = adminService;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<ActionResult<SystemAdminDashboardDto>> GetDashboard()
    {
        var data = await _adminService.GetDashboardAsync();
        return Ok(data);
    }

    // ── Users ─────────────────────────────────────────────────────────────
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<SystemAdminUserDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] bool? deactivated,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetUsersAsync(search, deactivated, page, pageSize);
        return Ok(result);
    }

    [HttpPost("users/{id:guid}/deactivate")]
    public async Task<ActionResult> DeactivateUser(Guid id, [FromQuery] Guid currentUserId)
    {
        var (succeeded, error) = await _adminService.DeactivateUserAsync(id, currentUserId);
        if (!succeeded)
            return BadRequest(new { error });
        return Ok();
    }

    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<ActionResult> ReactivateUser(Guid id)
    {
        var (succeeded, error) = await _adminService.ReactivateUserAsync(id);
        if (!succeeded)
            return BadRequest(new { error });
        return Ok();
    }

    // ── Roles ─────────────────────────────────────────────────────────────
    [HttpGet("users/{id:guid}/roles")]
    public async Task<ActionResult<UserRolesDataDto>> GetUserRolesData(Guid id)
    {
        var data = await _adminService.GetUserRolesDataAsync(id);
        return data == null ? NotFound() : Ok(data);
    }

    [HttpPut("users/{id:guid}/roles")]
    public async Task<ActionResult> UpdateUserRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        await _adminService.UpdateUserRoleAsync(id, request.SelectedRole);
        return Ok();
    }

    // ── Change Company ────────────────────────────────────────────────────
    [HttpGet("users/{id:guid}/change-company")]
    public async Task<ActionResult> GetChangeCompanyData(Guid id)
    {
        var validationError = await _adminService.ValidateChangeUserCompanyAsync(id);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        var data = await _adminService.GetChangeUserCompanyDataAsync(id);
        return data == null ? NotFound() : Ok(data);
    }

    [HttpPost("users/{id:guid}/change-company")]
    public async Task<ActionResult> ChangeUserCompany(
        Guid id,
        [FromBody] ChangeCompanyRequest request)
    {
        var (succeeded, error, companyName) =
            await _adminService.ChangeUserCompanyAsync(id, request.CompanyId, request.UpdatedBy);
        if (!succeeded)
            return BadRequest(new { error });
        return Ok(new { companyName });
    }

    // ── Tenants ───────────────────────────────────────────────────────────
    [HttpGet("tenants")]
    public async Task<ActionResult<TenantsListDto>> GetTenants(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var data = await _adminService.GetTenantsAsync(search, active, page, pageSize);
        return Ok(data);
    }

    [HttpPost("tenants/{id:guid}/toggle")]
    public async Task<ActionResult> ToggleTenantActive(Guid id, [FromQuery] string updatedBy)
    {
        var (succeeded, status, error) = await _adminService.ToggleTenantActiveAsync(id, updatedBy);
        if (!succeeded)
            return BadRequest(new { error });
        return Ok(new { status });
    }

    // ── Audit Logs ────────────────────────────────────────────────────────
    [HttpGet("audit-logs")]
    public async Task<ActionResult<AuditLogListDto>> GetAuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? entity,
        [FromQuery] string? action,
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var data = await _adminService.GetAuditLogsAsync(search, entity, action, tenantId, page, pageSize);
        return Ok(data);
    }

    // ── Create Tenant ────────────────────────────────────────────────────
    [HttpPost("tenants/create")]
    public async Task<ActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (await _adminService.SlugExistsAsync(request.Dto.Slug))
            return BadRequest(new { error = $"Slug '{request.Dto.Slug}' already exists." });

        var companyId = await _adminService.CreateTenantAsync(request.Dto, request.CreatedBy);

        if (request.OwnerUserId.HasValue)
            await _adminService.AssignTenantOwnerAsync(companyId, request.OwnerUserId.Value, request.CreatedBy);

        return Ok(new { companyId });
    }

    [HttpGet("tenants/slug-exists")]
    public async Task<ActionResult<bool>> SlugExists([FromQuery] string slug)
    {
        return Ok(await _adminService.SlugExistsAsync(slug));
    }

    [HttpGet("tenants/generate-slug")]
    public ActionResult<string> GenerateSlug([FromQuery] string name)
    {
        return Ok(_adminService.GenerateSlug(name));
    }

    [HttpGet("users-for-select")]
    public async Task<ActionResult> GetUsersForSelect()
    {
        var users = await _adminService.GetAllUsersForSelectAsync();
        return Ok(users);
    }

    // ── Create User ──────────────────────────────────────────────────────
    [HttpPost("users/create")]
    public async Task<ActionResult<CreateUserResultDto>> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        var result = await _adminService.CreateUserAsync(request.Dto, request.CreatedBy);
        if (!result.Succeeded)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("companies-for-select")]
    public async Task<ActionResult> GetCompaniesForSelect()
    {
        var companies = await _adminService.GetActiveCompaniesForSelectAsync();
        return Ok(companies);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────
public record UpdateRoleRequest(string SelectedRole);
public record ChangeCompanyRequest(Guid CompanyId, string UpdatedBy);
public record CreateTenantRequest(CreateTenantDto Dto, string CreatedBy, Guid? OwnerUserId);
public record CreateUserRequest(CreateSystemUserDto Dto, string CreatedBy);
