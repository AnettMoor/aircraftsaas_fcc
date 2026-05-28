using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Fleet.Domain.Entities;

public class Airport : BaseEntityWithMeta, ISoftDelete
{
    [Required]
    [StringLength(4)]
    public string IcaoCode { get; set; } = default!;
    
    [Required]
    [StringLength(3)]
    public string IataCode { get; set; } = default!;
    
    [Required]
    public LangStr Name { get; set; } = default!;
    
    [Required]
    public LangStr City { get; set; } = default!;
    
    [Required]
    public LangStr Country { get; set; } = default!;
    
    public double Latitude { get; set; }
    
    public double Longitude { get; set; }
    
    public int Elevation { get; set; } // feet
    
    // Soft delete
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation
    public ICollection<Aircraft>? Aircraft { get; set; }
    
    public bool IsDeleted => DeletedAt.HasValue;
    
    public void SoftDelete(string deletedBy)
    {
        DeletedBy = deletedBy;
        DeletedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        DeletedBy = null;
        DeletedAt = null;
    }
}
