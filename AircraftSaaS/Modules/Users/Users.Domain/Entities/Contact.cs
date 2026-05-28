using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Users.Domain.Entities;

public class Contact : BaseEntityWithMeta
{
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    
    public Guid ContactTypeId { get; set; }
    public ContactType? ContactType { get; set; }

    [StringLength(128, MinimumLength = 1)]
    public string ContactValue { get; set; } = default!;
}
