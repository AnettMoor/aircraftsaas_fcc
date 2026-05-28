using Shared.Contracts.Common;
using Users.Application.DTOs;

namespace Users.Application.Interfaces;

public interface ISystemAdminUsersService
{
    // ── Dashboard (users/tenants portion) ─────────────────────────────────────
    Task<SystemAdminDashboardDto> GetDashboardAsync();

    // ── Users ────────────────────────────────────────────────────────────────
    Task<PagedResult<SystemAdminUserDto>> GetUsersAsync(string? search, bool? deactivated, int page, int pageSize);
    Task<(bool Succeeded, string? Error)> DeactivateUserAsync(Guid userId, Guid currentUserId);
    Task<(bool Succeeded, string? Error)> ReactivateUserAsync(Guid userId);

    // ── User Roles ───────────────────────────────────────────────────────────
    Task<UserRolesDataDto?> GetUserRolesDataAsync(Guid userId);
    Task UpdateUserRoleAsync(Guid userId, string selectedRole);

    // ── User Company Assignment ──────────────────────────────────────────────
    Task<ChangeUserCompanyDataDto?> GetChangeUserCompanyDataAsync(Guid userId);
    Task<string?> ValidateChangeUserCompanyAsync(Guid userId);
    Task<(bool Succeeded, string? Error, string? CompanyName)> ChangeUserCompanyAsync(Guid userId, Guid companyId, string updatedBy);

    // ── Tenants ──────────────────────────────────────────────────────────────
    Task<TenantsListDto> GetTenantsAsync(string? search, bool? active, int page, int pageSize);
    Task<(bool Succeeded, string Status, string? Error)> ToggleTenantActiveAsync(Guid companyId, string updatedBy);

    // ── Audit Log ────────────────────────────────────────────────────────────
    Task<AuditLogListDto> GetAuditLogsAsync(string? search, string? entity, string? action, Guid? tenantId, int page, int pageSize);

    // ── Create Tenant ────────────────────────────────────────────────────────
    Task<bool> SlugExistsAsync(string slug);
    Task<Guid> CreateTenantAsync(CreateTenantDto dto, string createdBy);
    Task AssignTenantOwnerAsync(Guid companyId, Guid ownerUserId, string createdBy);
    Task<IEnumerable<UserSelectItemDto>> GetAllUsersForSelectAsync();

    // ── Create User ──────────────────────────────────────────────────────────
    Task<CreateUserResultDto> CreateUserAsync(CreateSystemUserDto dto, string createdBy);
    Task<IEnumerable<CompanySelectItemDto>> GetActiveCompaniesForSelectAsync();

    // ── Helpers ──────────────────────────────────────────────────────────────
    string GenerateSlug(string name);
}
