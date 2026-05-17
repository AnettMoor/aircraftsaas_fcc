using App.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace App.Domain.Identity;

public class AppUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    
    public string? LastName { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<AppUserCompany>? AppUserCompanies { get; set; }

    public ICollection<Person>? Persons { get; set; }
    
    public ICollection<AppRefreshToken>? RefreshTokens { get; set; }
    
    public ICollection<License>? Licenses { get; set; }
}
