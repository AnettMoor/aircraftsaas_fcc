using App.Application.DTOs;
using App.Domain;
using App.Domain.DTOs;
using App.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.SystemAdmin;

// ─────────────────────────────────────────────────────────────────────────────
// Shared pagination helper
// ─────────────────────────────────────────────────────────────────────────────

public class PaginationInfo
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}

// ─────────────────────────────────────────────────────────────────────────────
// Dashboard
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>System-wide dashboard statistics</summary>
public class SystemAdminDashboardViewModel
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
// Tenants
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>All-tenants list with search and pagination</summary>
public class SystemAdminTenantsViewModel
{
    public IEnumerable<TenantStatsDto> Tenants { get; set; } = new List<TenantStatsDto>();
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int TotalBookingsAcrossSystem { get; set; }

    // Filter
    public string? SearchQuery { get; set; }
    public bool? FilterActive { get; set; }

    // Pagination
    public PaginationInfo Pagination { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Users
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>All-users list with search and pagination</summary>
public class SystemAdminUsersViewModel
{
    public IEnumerable<SystemAdminUserDto> Users { get; set; } = new List<SystemAdminUserDto>();
    public int TotalUsers { get; set; }

    // Filter
    public string? SearchQuery { get; set; }
    public bool? FilterDeactivated { get; set; }

    // Pagination
    public PaginationInfo Pagination { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Role assignment
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>View model for the Edit-Roles page of a specific user</summary>
public class EditUserRolesViewModel
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;

    /// <summary>All available ASP.NET Identity roles in the system</summary>
    public IList<string> AllRoles { get; set; } = new List<string>();

    /// <summary>The single role currently assigned to this user</summary>
    public string AssignedRole { get; set; } = "Normal";

    /// <summary>Single role submitted from the form (bound on POST). A user can only have one role.</summary>
    [Required]
    public string SelectedRole { get; set; } = "Normal";

    // Company membership summary (read-only display)
    public IEnumerable<UserCompanyMembershipDto> CompanyMemberships { get; set; } =
        new List<UserCompanyMembershipDto>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Audit Log
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>System-wide audit log list with filters and pagination</summary>
public class SystemAdminAuditLogViewModel
{
    public IEnumerable<AuditLogDto> Logs { get; set; } = new List<AuditLogDto>();
    public int TotalLogs { get; set; }

    // Filters
    public string? SearchQuery { get; set; }
    public string? FilterEntity { get; set; }
    public string? FilterAction { get; set; }
    public Guid? FilterTenantId { get; set; }

    // For dropdowns
    public SelectList? TenantSelectList { get; set; }
    public IEnumerable<string> DistinctEntities { get; set; } = new List<string>();
    public IEnumerable<string> DistinctActions { get; set; } = new List<string>();

    // Pagination
    public PaginationInfo Pagination { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// All Bookings (system-wide)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>System-wide all-bookings list with search, status/tenant filters, and pagination</summary>
public class SystemAdminBookingsViewModel
{
    public IEnumerable<SystemAdminBookingDto> Bookings { get; set; } = new List<SystemAdminBookingDto>();
    public int TotalBookings { get; set; }

    // Filters
    public string? SearchQuery { get; set; }
    public string? FilterStatus { get; set; }
    public Guid? FilterTenantId { get; set; }

    // For dropdown
    public SelectList? TenantSelectList { get; set; }

    // Pagination
    public PaginationInfo Pagination { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// All Aircraft (system-wide)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>System-wide all-aircraft list with search, availability/tenant filters, and pagination</summary>
public class SystemAdminAircraftViewModel
{
    public IEnumerable<SystemAdminAircraftDto> Aircraft { get; set; } = new List<SystemAdminAircraftDto>();
    public int TotalAircraft { get; set; }

    // Filters
    public string? SearchQuery { get; set; }
    public Guid? FilterTenantId { get; set; }
    public bool? FilterAvailable { get; set; }

    // For dropdown
    public SelectList? TenantSelectList { get; set; }

    // Pagination
    public PaginationInfo Pagination { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Airports (system-wide management)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>System-wide airports management list with search, deleted-filter, and pagination</summary>
public class SystemAdminAirportsViewModel
{
    public IEnumerable<SystemAdminAirportDto> Airports { get; set; } = new List<SystemAdminAirportDto>();
    public int TotalAirports { get; set; }
    public int DeletedAirports { get; set; }

    // Filters
    public string? SearchQuery { get; set; }
    public bool ShowDeleted { get; set; }

    // Pagination
    public PaginationInfo Pagination { get; set; } = new();
}

/// <summary>Form view model for creating an airport (SystemAdmin)</summary>
public class SystemAdminCreateAirportViewModel
{
    [Required]
    [StringLength(4, MinimumLength = 4)]
    [Display(Name = "ICAO Code")]
    public string IcaoCode { get; set; } = default!;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    [Display(Name = "IATA Code")]
    public string IataCode { get; set; } = default!;

    [Required]
    [Display(Name = "Airport Name")]
    public string Name { get; set; } = default!;

    [Required]
    public string City { get; set; } = default!;

    [Required]
    public string Country { get; set; } = default!;

    [Display(Name = "Latitude")]
    public double Latitude { get; set; }

    [Display(Name = "Longitude")]
    public double Longitude { get; set; }

    [Display(Name = "Elevation (ft)")]
    public int Elevation { get; set; }
}

/// <summary>Form view model for editing an airport (SystemAdmin)</summary>
public class SystemAdminEditAirportViewModel
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(4, MinimumLength = 4)]
    [Display(Name = "ICAO Code")]
    public string IcaoCode { get; set; } = default!;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    [Display(Name = "IATA Code")]
    public string IataCode { get; set; } = default!;

    [Required]
    [Display(Name = "Airport Name")]
    public string Name { get; set; } = default!;

    [Required]
    public string City { get; set; } = default!;

    [Required]
    public string Country { get; set; } = default!;

    [Display(Name = "Latitude")]
    public double Latitude { get; set; }

    [Display(Name = "Longitude")]
    public double Longitude { get; set; }

    [Display(Name = "Elevation (ft)")]
    public int Elevation { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Create Tenant
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Form view model for creating a new tenant/company (SystemAdmin)</summary>
public class SystemAdminCreateTenantViewModel
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = default!;

    [StringLength(50)]
    [Display(Name = "Slug (URL identifier)")]
    public string? Slug { get; set; }

    [StringLength(100)]
    [EmailAddress]
    [Display(Name = "Contact Email")]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Range(1, 1000)]
    [Display(Name = "Max Users")]
    public int MaxUsers { get; set; } = 2;

    [Range(1, 1000)]
    [Display(Name = "Max Aircraft")]
    public int MaxAircraft { get; set; } = 3;

    [Range(1, 10000)]
    [Display(Name = "Max Bookings / Month")]
    public int MaxBookingsPerMonth { get; set; } = 20;

    [Display(Name = "Assign Owner (optional)")]
    public Guid? OwnerUserId { get; set; }

    /// <summary>Populated in GET for the owner user dropdown</summary>
    public SelectList? OwnerUserSelectList { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Create User
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Form view model for creating a new user (SystemAdmin)</summary>
public class SystemAdminCreateUserViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = default!;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = default!;

    [Required]
    [StringLength(100)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = default!;

    [Required]
    [StringLength(100)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = default!;

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = "Normal";

    [Display(Name = "Assign to Company (optional)")]
    public Guid? CompanyId { get; set; }

    /// <summary>Populated in GET for the company dropdown</summary>
    public SelectList? CompanySelectList { get; set; }

    // ── Inline new-company creation (shown when Role = CompanyOwner) ──

    /// <summary>When true, create a brand-new company and assign this user as its owner.</summary>
    [Display(Name = "Create a new company for this owner")]
    public bool CreateNewCompany { get; set; }

    [StringLength(200)]
    [Display(Name = "Company Name")]
    public string? NewCompanyName { get; set; }

    [StringLength(50)]
    [Display(Name = "Slug (URL identifier)")]
    public string? NewCompanySlug { get; set; }

    [StringLength(100)]
    [EmailAddress]
    [Display(Name = "Company Email")]
    public string? NewCompanyEmail { get; set; }

    [StringLength(50)]
    [Display(Name = "Company Phone")]
    public string? NewCompanyPhone { get; set; }

    [Display(Name = "Company Address")]
    public string? NewCompanyAddress { get; set; }

    [Range(1, 1000)]
    [Display(Name = "Max Users")]
    public int NewCompanyMaxUsers { get; set; } = 2;

    [Range(1, 1000)]
    [Display(Name = "Max Aircraft")]
    public int NewCompanyMaxAircraft { get; set; } = 3;

    [Range(1, 10000)]
    [Display(Name = "Max Bookings / Month")]
    public int NewCompanyMaxBookingsPerMonth { get; set; } = 20;
}

// ─────────────────────────────────────────────────────────────────────────────
// Change User Company Assignment
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Form view model for changing which company a user (typically CompanyOwner) is assigned to</summary>
public class ChangeUserCompanyViewModel
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public IList<string> Roles { get; set; } = new List<string>();

    /// <summary>Current company memberships for display</summary>
    public IEnumerable<UserCompanyMembershipDto> CurrentMemberships { get; set; } =
        new List<UserCompanyMembershipDto>();

    /// <summary>The company to assign the user to (selected from dropdown)</summary>
    [Required]
    [Display(Name = "Assign to Company")]
    public Guid? SelectedCompanyId { get; set; }

    /// <summary>All available companies for the dropdown</summary>
    public SelectList? CompanySelectList { get; set; }
}
