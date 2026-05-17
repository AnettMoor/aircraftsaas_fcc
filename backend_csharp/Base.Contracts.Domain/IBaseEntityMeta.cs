using System.ComponentModel.DataAnnotations;

namespace Base.Contracts.Domain;

public interface IBaseEntityMeta
{
    [MaxLength(128)]
    public string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    
    [MaxLength(128)]
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
