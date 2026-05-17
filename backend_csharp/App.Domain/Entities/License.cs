using System.ComponentModel.DataAnnotations;
using App.Domain.Identity;
using Base.Domain;

namespace App.Domain.Entities;

public class License : BaseEntity, ISoftDelete
{
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    
    [Required]
    [StringLength(50)]
    public string LicenseNumber { get; set; } = default!;
    
    [Required]
    public LangStr LicenseType { get; set; } = default!; // LAPL(A), LAPL(H), PPL, CPL, ATPL
    
    public DateTime IssueDate { get; set; }
    
    public DateTime ExpiryDate { get; set; }
    
    public LangStr IssuingAuthority { get; set; } = default!;
    
    public bool IsValid => ExpiryDate > DateTime.UtcNow;
    
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
