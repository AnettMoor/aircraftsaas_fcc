using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Users.Domain.Entities;

public class ContactType : BaseEntityWithMeta
{
    [StringLength(128, MinimumLength = 1)] 
    public LangStr ContactTypeName { get; set; } = default!;
    
    public ICollection<Contact>? Contacts { get; set; }
}
