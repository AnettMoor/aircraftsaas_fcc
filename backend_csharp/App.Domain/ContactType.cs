using System.ComponentModel.DataAnnotations;
using Base.Domain;

namespace App.Domain;

public class ContactType : BaseEntityWithMeta
{
    [StringLength(128, MinimumLength = 1)] 
    public LangStr ContactTypeName { get; set; } = default!;
    
    public ICollection<Contact>? Contacts { get; set; }

}
