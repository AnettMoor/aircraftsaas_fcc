using System.ComponentModel.DataAnnotations;
using App.Domain.Identity;
using Base.Domain;

namespace App.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid TenantId { get; set; }
    
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    
    [Required]
    [StringLength(100)]
    public string EntityName { get; set; } = default!;
    
    public Guid EntityId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Action { get; set; } = default!; // Created, Updated, Deleted
    
    public string? OldValues { get; set; } // JSON
    
    public string? NewValues { get; set; } // JSON
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [StringLength(50)]
    public string IpAddress { get; set; } = default!;
    
    [StringLength(500)]
    public string? Details { get; set; }
}
