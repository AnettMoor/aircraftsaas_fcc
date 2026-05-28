using System.ComponentModel.DataAnnotations;

namespace Users.Api.DTOs.Identity;

public class LoginInfo
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}
