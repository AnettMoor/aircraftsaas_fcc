using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Users.Domain.Entities;

public class Company : BaseEntityWithMeta, ISoftDelete
{
    public LangStr CompanyName { get; set; } = default!;
    
    [StringLength(50)]
    public string Slug { get; set; } = default!; // For URL: company.platform.com/slug
    
    public bool IsActive { get; set; } = true;
    
    // Usage limits
    public int MaxUsers { get; set; } = 2;
    public int MaxAircraft { get; set; } = 3;
    public int MaxBookingsPerMonth { get; set; } = 20;
    
    // Contact info
    public LangStr? Address { get; set; }
    
    [StringLength(50)]
    public string? Phone { get; set; }
    
    [StringLength(100)]
    public string? Email { get; set; }
    
    // Domain for tenant resolution (e.g., company.platform.com)
    [StringLength(100)]
    public string? Domain { get; set; }
    
    // Soft delete
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation — same module only
    public ICollection<AppUserCompany>? AppUserCompanies { get; set; }
    
    public ICollection<Person>? Persons { get; set; }
    
    public ICollection<Contact>? Contacts { get; set; }
    
    public ICollection<ContactType>? ContactTypes { get; set; }
    
    public bool IsDeleted => DeletedAt.HasValue;
    
    public void SoftDelete(string deletedBy)
    {
        DeletedBy = deletedBy;
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
    }

    public void Restore()
    {
        DeletedBy = null;
        DeletedAt = null;
        IsActive = true;
    }
}
