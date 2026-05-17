using App.Domain.Identity;
using Base.Contracts.Domain;
using Base.Domain;

namespace App.Domain;

public class AppUserCompany : BaseEntityWithMeta, IBaseEntityAppUser<Guid>
{
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public EAppUserRoleInCompany AppUserRoleInCompany { get; set; } = EAppUserRoleInCompany.Normal;
    
    /// <summary>
    /// Alias property for compatibility — NOT a separate DB column.
    /// Configured as Ignored in AppDbContext.OnModelCreating (Fluent API).
    /// </summary>
    public EAppUserRoleInCompany Role { get => AppUserRoleInCompany; set => AppUserRoleInCompany = value; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
