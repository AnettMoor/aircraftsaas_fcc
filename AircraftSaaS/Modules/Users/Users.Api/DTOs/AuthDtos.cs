using System.ComponentModel.DataAnnotations;

namespace Users.Api.DTOs;

/// <summary>
/// Auth-related v1 public API types.
/// Login/Register types are already in Users.Api.DTOs.Identity.
/// These cover any additional auth flow types not present there.
/// </summary>
public class RegisterCompanyRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = default!;

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string CompanyName { get; set; } = default!;

    [Phone]
    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}
