using System.ComponentModel.DataAnnotations;
using App.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApp.ViewModels.Admin;

// ──────────────────────────────────────────────
// Person ViewModels
// ──────────────────────────────────────────────

public class PersonCreateViewModel
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = default!;

    [Required]
    [Display(Name = "User")]
    public Guid AppUserId { get; set; }

    [Display(Name = "Company")]
    public Guid? CompanyId { get; set; }

    // Select lists – populated by the controller, never bound from the form
    public SelectList? AppUserSelectList { get; set; }
    public SelectList? CompanySelectList { get; set; }
}

public class PersonEditViewModel
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = default!;

    [Required]
    [Display(Name = "User")]
    public Guid AppUserId { get; set; }

    [Display(Name = "Company")]
    public Guid? CompanyId { get; set; }

    // Select lists – populated by the controller, never bound from the form
    public SelectList? AppUserSelectList { get; set; }
    public SelectList? CompanySelectList { get; set; }
}

// ──────────────────────────────────────────────
// Contact ViewModels
// ──────────────────────────────────────────────

public class ContactCreateViewModel
{
    [Required]
    [Display(Name = "Person")]
    public Guid PersonId { get; set; }

    [Required]
    [Display(Name = "Contact Type")]
    public Guid ContactTypeId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [Display(Name = "Contact Value")]
    public string ContactValue { get; set; } = default!;

    // Select lists – populated by the controller, never bound from the form
    public SelectList? ContactTypeSelectList { get; set; }
    public SelectList? PersonSelectList { get; set; }
}

public class ContactEditViewModel
{
    public Guid Id { get; set; }

    [Required]
    [Display(Name = "Person")]
    public Guid PersonId { get; set; }

    [Required]
    [Display(Name = "Contact Type")]
    public Guid ContactTypeId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [Display(Name = "Contact Value")]
    public string ContactValue { get; set; } = default!;

    // Select lists – populated by the controller, never bound from the form
    public SelectList? ContactTypeSelectList { get; set; }
    public SelectList? PersonSelectList { get; set; }
}

// ──────────────────────────────────────────────
// AppUserCompany ViewModels
// ──────────────────────────────────────────────

public class AppUserCompanyCreateViewModel
{
    [Required]
    [Display(Name = "User")]
    public Guid AppUserId { get; set; }

    [Required]
    [Display(Name = "Company")]
    public Guid CompanyId { get; set; }

    [Required]
    [Display(Name = "Role")]
    public EAppUserRoleInCompany AppUserRoleInCompany { get; set; }

    // Select lists – populated by the controller, never bound from the form
    public SelectList? AppUserSelectList { get; set; }
    public SelectList? CompanySelectList { get; set; }
}

public class AppUserCompanyEditViewModel
{
    public Guid Id { get; set; }

    [Required]
    [Display(Name = "User")]
    public Guid AppUserId { get; set; }

    [Required]
    [Display(Name = "Company")]
    public Guid CompanyId { get; set; }

    [Required]
    [Display(Name = "Role")]
    public EAppUserRoleInCompany AppUserRoleInCompany { get; set; }

    // Select lists – populated by the controller, never bound from the form
    public SelectList? AppUserSelectList { get; set; }
    public SelectList? CompanySelectList { get; set; }
}
