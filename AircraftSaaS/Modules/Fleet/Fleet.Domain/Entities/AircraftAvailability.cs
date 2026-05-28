using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Fleet.Domain.Entities;

public class AircraftAvailability : BaseEntity, ISoftDelete
{
    public Guid AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }
    
    /// <summary>
    /// Optional link to the booking that created this availability block.
    /// Cross-module FK to Booking module — Guid only, NO navigation property.
    /// </summary>
    public Guid? BookingId { get; set; }
    
    public DateTime StartDateTime { get; set; }
    
    public DateTime EndDateTime { get; set; }
    
    [StringLength(50)]
    public string AvailabilityType { get; set; } = default!; // Available, Maintenance, Blocked, Booked
    
    [StringLength(500)]
    public string? Reason { get; set; }
    
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
