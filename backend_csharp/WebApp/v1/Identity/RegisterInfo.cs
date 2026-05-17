using System.ComponentModel.DataAnnotations;

namespace WebApp.v1.Identity;

public class RegisterInfo
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
    public string Firstname { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Lastname { get; set; } = default!;
}
