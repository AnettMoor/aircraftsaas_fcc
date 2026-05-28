using Booking.Domain.Enums;
using Shared.Kernel.Domain;

namespace Booking.Domain.Entities;

public class Booking : BaseEntityWithMeta, ISoftDelete, IAppUserId<Guid>, ICompanyEntity
{
    /// <summary>
    /// Cross-module FK to Fleet — Guid only, NO navigation property.
    /// </summary>
    public Guid AircraftId { get; set; }
    
    /// <summary>
    /// Cross-module FK to Users (pilot) — Guid only, NO navigation property.
    /// </summary>
    public Guid PilotId { get; set; }
    
    /// <summary>
    /// Maps to PilotId for BaseRepository IDOR filtering.
    /// Configured as Ignored in DbContext.OnModelCreating (Fluent API).
    /// </summary>
    public Guid AppUserId { get => PilotId; set => PilotId = value; }
    
    // Customer (who booked for themselves or others)
    /// <summary>
    /// Cross-module FK to Users — Guid only, NO navigation property.
    /// </summary>
    public Guid? CustomerId { get; set; }
    
    public DateTime StartDateTime { get; set; }
    
    public DateTime EndDateTime { get; set; }
    
    public EBookingStatus Status { get; set; } = EBookingStatus.Requested;
    
    public LangStr? Purpose { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    public LangStr? RejectionReason { get; set; }
    
    public DateTime? ApprovedAt { get; set; }
    
    public DateTime? PaidAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    
    // Soft delete
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Tenant — cross-module FK to Users (Guid only, NO Company navigation property)
    public Guid CompanyId { get; set; }
    
    // Navigation — same module only
    public ICollection<Payment>? Payments { get; set; }
    public ICollection<Review>? Reviews { get; set; }
    
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
    
    // Validation methods — single source of truth for status transitions
    public bool CanApprove() => Status == EBookingStatus.Requested;
    
    public bool CanReject() => Status == EBookingStatus.Requested;
    
    public bool CanPay() => Status == EBookingStatus.Approved;
    
    public bool CanCancel() => Status != EBookingStatus.Completed &&
                              Status != EBookingStatus.Cancelled;
    
    public bool CanComplete() => Status == EBookingStatus.Paid;
}
