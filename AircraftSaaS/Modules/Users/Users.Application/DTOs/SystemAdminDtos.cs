using Shared.Contracts.Common;
using Users.Domain.Enums;

namespace Users.Application.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Dashboard (aggregated from all modules — composed in controller)
// ─────────────────────────────────────────────────────────────────────────────

public class SystemAdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int TotalBookings { get; set; }
    public int TotalAircraft { get; set; }
    public int TotalAirports { get; set; }
    public IEnumerable<TenantStatsDto> TopTenantsByBookings { get; set; } = new List<TenantStatsDto>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Tenant Stats
// ─────────────────────────────────────────────────────────────────────────────

public class TenantStatsDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public int AircraftCount { get; set; }
    public int BookingCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? OwnerName { get; set; }
}

public class TenantsListDto
{
    public PagedResult<TenantStatsDto> Tenants { get; set; } = new();
    public int ActiveTenants { get; set; }
    public int TotalBookingsAcrossSystem { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Users
// ─────────────────────────────────────────────────────────────────────────────

public class SystemAdminUserDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public int BookingCount { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public bool IsDeactivated { get; set; }
    public string? CompanyName { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// User Roles
// ─────────────────────────────────────────────────────────────────────────────

public class UserRolesDataDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public IList<string> AllRoles { get; set; } = new List<string>();
    public string AssignedRole { get; set; } = "Normal";
    public IEnumerable<UserCompanyMembershipDto> CompanyMemberships { get; set; } = new List<UserCompanyMembershipDto>();
}

public class UserCompanyMembershipDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public EAppUserRoleInCompany Role { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Change User Company
// ─────────────────────────────────────────────────────────────────────────────

public class ChangeUserCompanyDataDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public IList<string> Roles { get; set; } = new List<string>();
    public IEnumerable<UserCompanyMembershipDto> CurrentMemberships { get; set; } = new List<UserCompanyMembershipDto>();
    public Guid? CurrentCompanyId { get; set; }
    public IEnumerable<CompanySelectItemDto> ActiveCompanies { get; set; } = new List<CompanySelectItemDto>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Audit Log
// ─────────────────────────────────────────────────────────────────────────────

public class AuditLogListDto
{
    public PagedResult<AuditLogDto> Logs { get; set; } = new();
    public IEnumerable<string> DistinctEntities { get; set; } = new List<string>();
    public IEnumerable<string> DistinctActions { get; set; } = new List<string>();
    public IEnumerable<CompanySelectItemDto> Companies { get; set; } = new List<CompanySelectItemDto>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Create Tenant
// ─────────────────────────────────────────────────────────────────────────────

public class CreateTenantDto
{
    public string CompanyName { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int MaxUsers { get; set; }
    public int MaxAircraft { get; set; }
    public int MaxBookingsPerMonth { get; set; }
    public Guid? OwnerUserId { get; set; }
}

public class UserSelectItemDto
{
    public Guid Id { get; set; }
    public string Display { get; set; } = default!;
}

// ─────────────────────────────────────────────────────────────────────────────
// Create User
// ─────────────────────────────────────────────────────────────────────────────

public class CreateSystemUserDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Role { get; set; } = "Normal";
    public Guid? CompanyId { get; set; }

    // New company creation
    public bool CreateNewCompany { get; set; }
    public string? NewCompanyName { get; set; }
    public string? NewCompanySlug { get; set; }
    public string? NewCompanyEmail { get; set; }
    public string? NewCompanyPhone { get; set; }
    public string? NewCompanyAddress { get; set; }
    public int NewCompanyMaxUsers { get; set; } = 2;
    public int NewCompanyMaxAircraft { get; set; } = 3;
    public int NewCompanyMaxBookingsPerMonth { get; set; } = 20;
}

public class CreateUserResultDto
{
    public bool Succeeded { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();
    public string? Email { get; set; }
    public string? Role { get; set; }
    public Guid? AssignedCompanyId { get; set; }
    public string? NewCompanyName { get; set; }
}
