using System.ComponentModel.DataAnnotations;

namespace Users.Api.DTOs.Identity;

public class LogoutInfo
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}
