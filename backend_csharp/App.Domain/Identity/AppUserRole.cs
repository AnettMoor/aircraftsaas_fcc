using Microsoft.AspNetCore.Identity;

namespace App.Domain.Identity;

/// <summary>
/// FK relationships configured via Fluent API in AppDbContext.OnModelCreating.
/// </summary>
public class AppUserRole : IdentityUserRole<Guid>
{
    public AppUser AppUser { get; set; } = default!;

    public AppRole AppRole { get; set; } = default!;
}
