using System.ComponentModel.DataAnnotations;
using App.Application.DTOs;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebApp.ViewModels.Maintenance;

/// <summary>
/// Maintenance index/list view model
/// </summary>
public class MaintenanceIndexViewModel
{
    public IEnumerable<MaintenanceRecordDto> Records { get; set; } = new List<MaintenanceRecordDto>();

    [ValidateNever]
    public IEnumerable<AircraftDto> Aircraft { get; set; } = new List<AircraftDto>();

    /// <summary>
    /// Optional filter by aircraft ID.
    /// </summary>
    public Guid? FilterAircraftId { get; set; }
}

/// <summary>
/// Maintenance details view model
/// </summary>
public class MaintenanceDetailsViewModel
{
    public MaintenanceRecordDto Record { get; set; } = default!;
}

/// <summary>
/// Maintenance create/edit form view model.
/// Replaces the inline MaintenanceFormModel that was in the controller,
/// and absorbs ViewBag data (Aircraft list, Record ID) into proper properties.
/// </summary>
public class MaintenanceFormViewModel
{
    /// <summary>
    /// Record ID — null for create, populated for edit.
    /// </summary>
    public Guid? Id { get; set; }

    [Required]
    [Display(Name = "Aircraft")]
    public Guid AircraftId { get; set; }

    [Required]
    [Display(Name = "Maintenance Date")]
    [DataType(DataType.Date)]
    public DateTime MaintenanceDate { get; set; }

    [Required]
    [Display(Name = "Maintenance Type")]
    public string MaintenanceType { get; set; } = "";

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Performed By")]
    public string? PerformedBy { get; set; }

    [Display(Name = "Airframe Hours at Maintenance")]
    public int AirframeHoursAtMaintenance { get; set; }

    [Display(Name = "Next Due Date")]
    [DataType(DataType.Date)]
    public DateTime? NextDueDate { get; set; }

    [Display(Name = "Next Due Hours")]
    public int? NextDueHours { get; set; }

    [Display(Name = "Cost ($)")]
    [DataType(DataType.Currency)]
    public decimal Cost { get; set; }

    [Display(Name = "Completed")]
    public bool IsCompleted { get; set; } = true;

    /// <summary>
    /// Available aircraft for the dropdown.
    /// </summary>
    [ValidateNever]
    public IEnumerable<AircraftDto> Aircraft { get; set; } = new List<AircraftDto>();
}
