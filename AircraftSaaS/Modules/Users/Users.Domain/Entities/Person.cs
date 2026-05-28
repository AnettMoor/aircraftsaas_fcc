using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;
using Users.Domain.Identity;

namespace Users.Domain.Entities;

public class Person : BaseEntityWithMeta, IBaseEntityAppUser<Guid>
{
    [StringLength(128, MinimumLength = 1)]
    public string FirstName { get; set; } = default!;

    [StringLength(128, MinimumLength = 1)]
    public string LastName { get; set; } = default!;

    public ICollection<Contact>? Contacts { get; set; }

    // either company OR normal user (pilot)
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
}
