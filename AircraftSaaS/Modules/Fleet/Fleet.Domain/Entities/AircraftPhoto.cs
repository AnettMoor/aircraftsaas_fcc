using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Fleet.Domain.Entities;

public class AircraftPhoto : BaseEntity, ISoftDelete
{
    public Guid AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }
    
    [Required]
    [StringLength(500)]
    public string ImageUrl { get; set; } = default!;
    
    // Alias for ImageUrl for backward compatibility
    public string Url => ImageUrl;
    
    [StringLength(200)]
    public string? Description { get; set; }
    
    public bool IsPrimary { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // Soft delete
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
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
