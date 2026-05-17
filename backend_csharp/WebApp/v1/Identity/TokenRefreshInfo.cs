using System.ComponentModel.DataAnnotations;

namespace WebApp.v1.Identity;

public class TokenRefreshInfo
{
    [Required]
    public string Jwt { get; set; } = default!;

    [Required]
    public string RefreshToken { get; set; } = default!;
}
