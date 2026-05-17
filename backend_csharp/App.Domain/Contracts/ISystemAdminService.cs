using App.Domain.DTOs;

namespace App.Domain.Contracts;

public interface ISystemAdminService
{
    // ── Dashboard ────────────────────────────────────────────────────────────
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
    /// <summary>Returns error message or null on success.</summary>
    Task<string?> ValidateChangeUserCompanyAsync(Guid userId);
    Task<(bool Succeeded, string? Error, string? CompanyName)> ChangeUserCompanyAsync(Guid userId, Guid companyId, string updatedBy);

    // ── Tenants ──────────────────────────────────────────────────────────────
    Task<TenantsListDto> GetTenantsAsync(string? search, bool? active, int page, int pageSize);
    Task<(bool Succeeded, string Status, string? Error)> ToggleTenantActiveAsync(Guid companyId, string updatedBy);

    // ── Audit Log ────────────────────────────────────────────────────────────
    Task<AuditLogListDto> GetAuditLogsAsync(string? search, string? entity, string? action, Guid? tenantId, int page, int pageSize);

    // ── All Bookings (system-wide) ───────────────────────────────────────────
    Task<BookingsListDto> GetAllBookingsAsync(string? search, string? status, Guid? tenantId, int page, int pageSize);

    // ── All Aircraft (system-wide) ───────────────────────────────────────────
    Task<AircraftListDto> GetAllAircraftAsync(string? search, Guid? tenantId, bool? available, int page, int pageSize);

    // ── Airports ─────────────────────────────────────────────────────────────
    Task<AirportsListDto> GetAirportsAsync(string? search, bool showDeleted, int page, int pageSize);
    Task<AirportEditDto?> GetAirportForEditAsync(Guid id);
    Task<bool> AirportExistsByIcaoCodeAsync(string icaoCode, Guid? excludeId = null);
    Task<bool> HasActiveAircraftAtAirportAsync(Guid airportId);

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
