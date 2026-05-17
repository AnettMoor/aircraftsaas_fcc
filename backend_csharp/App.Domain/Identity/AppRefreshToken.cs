using System.ComponentModel.DataAnnotations;
using Base.Domain;

namespace App.Domain.Identity;

public class AppRefreshToken : BaseEntity
{
    [MaxLength(64)]
    public string RefreshToken { get; set; } = Guid.NewGuid().ToString();
    public DateTime ExpirationDT { get; set; } = DateTime.UtcNow.AddDays(7);

    [MaxLength(64)]
    public string? PreviousRefreshToken { get; set; }
    public DateTime PreviousExpirationDT { get; set; } = DateTime.MinValue;
    
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
}