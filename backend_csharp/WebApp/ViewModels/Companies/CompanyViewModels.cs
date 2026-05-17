using System.ComponentModel.DataAnnotations;
using App.Application.DTOs;

namespace WebApp.ViewModels.Companies;

/// <summary>
/// Company index/list view model
/// </summary>
public class CompanyIndexViewModel
{
    public IEnumerable<CompanyDto> Companies { get; set; } = new List<CompanyDto>();
}

/// <summary>
/// Company details view model
/// </summary>
public class CompanyDetailsViewModel
{
    public CompanyDto Company { get; set; } = default!;
}

/// <summary>
/// Company create form view model
/// </summary>
public class CompanyCreateViewModel
{
    [Required]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = default!;

    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Display(Name = "Email")]
    [EmailAddress]
    public string? Email { get; set; }
}

/// <summary>
/// Company edit form view model
/// </summary>
public class CompanyEditViewModel
{
    public Guid Id { get; set; }

    /// <summary>
    /// Read-only company data for display in the edit view.
    /// </summary>
    public CompanyDto? Company { get; set; }

    [Required]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = default!;

    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Display(Name = "Email")]
    [EmailAddress]
    public string? Email { get; set; }
}
