using System.ComponentModel.DataAnnotations;
using App.Application.DTOs;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebApp.ViewModels.Reviews;

/// <summary>
/// Reviews index/list view model
/// </summary>
public class ReviewIndexViewModel
{
    public IEnumerable<ReviewDto> Reviews { get; set; } = new List<ReviewDto>();
}

/// <summary>
/// Reviews filtered by aircraft view model
/// </summary>
public class ReviewByAircraftViewModel
{
    public IEnumerable<ReviewDto> Reviews { get; set; } = new List<ReviewDto>();
    public AircraftDto? Aircraft { get; set; }
}

/// <summary>
/// Review details view model
/// </summary>
public class ReviewDetailsViewModel
{
    public ReviewDto Review { get; set; } = default!;
}

/// <summary>
/// Review create form view model
/// </summary>
public class ReviewCreateViewModel
{
    [Required]
    public Guid AircraftId { get; set; }

    public Guid BookingId { get; set; }

    [Required]
    [Range(1, 5)]
    [Display(Name = "Rating")]
    public int Rating { get; set; } = 5;

    [Display(Name = "Comment")]
    public string? Comment { get; set; }

    [Display(Name = "Review Type")]
    public string? ReviewType { get; set; }

    /// <summary>
    /// Aircraft data for display in the create form.
    /// </summary>
    [ValidateNever]
    public AircraftDto? Aircraft { get; set; }
}

/// <summary>
/// Review edit form view model
/// </summary>
public class ReviewEditViewModel
{
    public Guid Id { get; set; }

    [Required]
    [Range(1, 5)]
    [Display(Name = "Rating")]
    public int Rating { get; set; }

    [Display(Name = "Comment")]
    public string? Comment { get; set; }

    [Display(Name = "Review Type")]
    public string? ReviewType { get; set; }

    /// <summary>
    /// Read-only review data for display (aircraft name, author, etc.)
    /// </summary>
    [ValidateNever]
    public ReviewDto? Review { get; set; }
}
