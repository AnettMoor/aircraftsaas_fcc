using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Fleet.Domain.Entities;

public class InsurancePolicy : BaseEntity, ISoftDelete
{
    public Guid AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }
    
    [Required]
    [StringLength(50)]
    public string PolicyNumber { get; set; } = default!;
    
    [Required]
    public LangStr InsuranceProvider { get; set; } = default!;
    
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
    
    public decimal CoverageAmount { get; set; }
    
    public LangStr CoverageType { get; set; } = default!; // Liability, Hull, Comprehensive
    
    public bool IsActive => StartDate <= DateTime.UtcNow && EndDate >= DateTime.UtcNow;
    
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
