using System.ComponentModel.DataAnnotations;

namespace WebApp.v1.Identity;

public class LogoutInfo
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}
