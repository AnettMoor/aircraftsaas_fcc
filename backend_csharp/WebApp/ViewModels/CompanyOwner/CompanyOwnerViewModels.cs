using System.ComponentModel.DataAnnotations;
using App.Application.DTOs;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebApp.ViewModels.CompanyOwner;

/// <summary>
/// Dashboard view model for Company Owner
/// </summary>
public class DashboardViewModel
{
    public CompanyDto? Company { get; set; }
    public int TotalAircraft { get; set; }
    public int AvailableAircraft { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int TotalUsers { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public IEnumerable<AircraftDto> RecentAircraft { get; set; } = new List<AircraftDto>();
    public IEnumerable<BookingDto> PendingApprovalBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> RecentBookings { get; set; } = new List<BookingDto>();
}

/// <summary>
/// Aircraft list view model
/// </summary>
public class AircraftListViewModel
{
    public IEnumerable<AircraftDto> Aircraft { get; set; } = new List<AircraftDto>();
    public IEnumerable<AircraftDto> DeletedAircraft { get; set; } = new List<AircraftDto>();
    public AircraftSearchDto? SearchModel { get; set; }
}

/// <summary>
/// Aircraft edit view model
/// </summary>
public class AircraftEditViewModel
{
    public Guid Id { get; set; }
    [Required]
    [Display(Name = "Registration Number")]
    public string RegistrationNumber { get; set; } = default!;
    
    [Required]
    public string Make { get; set; } = default!;
    
    [Required]
    public string Model { get; set; } = default!;
    
    [Required]
    [Display(Name = "Year")]
    public int Year { get; set; }
    
    [Required]
    public string Category { get; set; } = default!;
    
    [Display(Name = "Total Airspeed Hours")]
    public int TotalAirspeedHours { get; set; }
    
    [Required]
    [Display(Name = "Hourly Rate ($)")]
    public decimal HourlyRate { get; set; }
    
    [Required]
    [Display(Name = "Base Airport")]
    public Guid? BaseAirportId { get; set; }
    
    [ValidateNever]
    public IEnumerable<AirportDto> Airports { get; set; } = new List<AirportDto>();
    
    [Display(Name = "Custom Airport Name")]
    public string? CustomAirportName { get; set; }
    
    public string? Description { get; set; }
    
    [Display(Name = "Available for Booking")]
    public bool IsAvailable { get; set; } = true;
}

/// <summary>
/// Booking management view model
/// </summary>
public class BookingManagementViewModel
{
    public IEnumerable<BookingDto> AllBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> PendingBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> ApprovedBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> PaidBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> CompletedBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> CancelledBookings { get; set; } = new List<BookingDto>();
}

/// <summary>
/// Company settings view model
/// </summary>
public class CompanySettingsViewModel
{
    public CompanyDto? Company { get; set; }
    public UpdateCompanyDto? UpdateModel { get; set; }
}

/// <summary>
/// Maintenance view model
/// </summary>
public class MaintenanceListViewModel
{
    public IEnumerable<MaintenanceRecordDto> Records { get; set; } = new List<MaintenanceRecordDto>();
    public IEnumerable<AircraftDto> Aircraft { get; set; } = new List<AircraftDto>();
    public Guid? FilterAircraftId { get; set; }
}

/// <summary>
/// Create maintenance record view model
/// </summary>
public class MaintenanceEditViewModel
{
    public Guid Id { get; set; }
    
    [Required]
    [Display(Name = "Aircraft")]
    public Guid? AircraftId { get; set; }
    
    [Required]
    [Display(Name = "Maintenance Date")]
    public DateTime MaintenanceDate { get; set; } = DateTime.Today;
    
    [Required]
    [Display(Name = "Maintenance Type")]
    public string MaintenanceType { get; set; } = default!;
    
    public string? Description { get; set; }
    
    [Display(Name = "Performed By")]
    public string? PerformedBy { get; set; }
    
    [Display(Name = "Airframe Hours")]
    public int AirframeHoursAtMaintenance { get; set; }
    
    [Display(Name = "Next Due Date")]
    public DateTime? NextDueDate { get; set; }
    
    [Display(Name = "Next Due Hours")]
    public int? NextDueHours { get; set; }
    
    public decimal Cost { get; set; }
    
    [Display(Name = "Completed")]
    public bool IsCompleted { get; set; } = true;
    
    [ValidateNever]
    public IEnumerable<AircraftDto> Aircraft { get; set; } = new List<AircraftDto>();
}