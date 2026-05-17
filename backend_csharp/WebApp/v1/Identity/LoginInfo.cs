using System.ComponentModel.DataAnnotations;

namespace WebApp.v1.Identity;

public class LoginInfo
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}
