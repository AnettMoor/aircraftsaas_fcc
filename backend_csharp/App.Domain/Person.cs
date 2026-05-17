using System.ComponentModel.DataAnnotations;
using App.Domain.Identity;
using Base.Contracts.Domain;
using Base.Domain;

namespace App.Domain;

public class Person : BaseEntityWithMeta, IBaseEntityAppUser<Guid>, IAppUserId
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
