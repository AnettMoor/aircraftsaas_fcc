using System.ComponentModel.DataAnnotations;

namespace Users.Api.DTOs.Identity;

public class TokenRefreshInfo
{
    [Required]
    public string Jwt { get; set; } = default!;

    [Required]
    public string RefreshToken { get; set; } = default!;
}
