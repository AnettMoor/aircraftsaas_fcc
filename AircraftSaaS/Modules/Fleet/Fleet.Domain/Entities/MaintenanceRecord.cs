using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Fleet.Domain.Entities;

public class MaintenanceRecord : BaseEntityWithMeta, ISoftDelete
{
    public Guid AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }
    
    public DateTime MaintenanceDate { get; set; }
    
    [Required]
    public LangStr MaintenanceType { get; set; } = default!; // Annual, 100hr, Preventive, Repair
    
    // Status: Scheduled, InProgress, Completed, Cancelled
    public LangStr Status { get; set; } = new LangStr("Scheduled");
    
    // Start and end dates for maintenance blocks (used for booking validation)
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    public LangStr Description { get; set; } = default!;
    
    [Required]
    [StringLength(100)]
    public string PerformedBy { get; set; } = default!;
    
    /// <summary>
    /// Cross-module FK to Users — Guid only, NO navigation property.
    /// </summary>
    public Guid? PerformedByUserId { get; set; }
    
    public int AirframeHoursAtMaintenance { get; set; }
    
    public DateTime? NextDueDate { get; set; }
    
    public int? NextDueHours { get; set; }
    
    public decimal Cost { get; set; }
    
    public bool IsCompleted { get; set; } = true;
    
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
