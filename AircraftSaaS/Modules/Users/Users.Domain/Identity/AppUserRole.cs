using Microsoft.AspNetCore.Identity;

namespace Users.Domain.Identity;

/// <summary>
/// FK relationships configured via Fluent API in DbContext.OnModelCreating.
/// </summary>
public class AppUserRole : IdentityUserRole<Guid>
{
    public AppUser AppUser { get; set; } = default!;

    public AppRole AppRole { get; set; } = default!;
}
