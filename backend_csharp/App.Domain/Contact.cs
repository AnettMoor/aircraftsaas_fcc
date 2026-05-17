using System.ComponentModel.DataAnnotations;
using Base.Domain;

namespace App.Domain;

public class Contact : BaseEntityWithMeta
{
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    
    public Guid ContactTypeId { get; set; }
    public ContactType? ContactType { get; set; }

    [StringLength(128, MinimumLength = 1)]
    public string ContactValue { get; set; } = default!;
}
