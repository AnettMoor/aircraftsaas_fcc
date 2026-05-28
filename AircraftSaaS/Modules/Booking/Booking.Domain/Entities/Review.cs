using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Booking.Domain.Entities;

public class Review : BaseEntity, ISoftDelete, IAppUserId<Guid>
{
    /// <summary>
    /// Cross-module FK to Fleet — Guid only, NO navigation property.
    /// </summary>
    public Guid AircraftId { get; set; }
    
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    
    /// <summary>
    /// Cross-module FK to Users (author) — Guid only, NO navigation property.
    /// </summary>
    public Guid AuthorId { get; set; }
    
    /// <summary>
    /// Maps to AuthorId for BaseRepository IDOR filtering.
    /// Configured as Ignored in DbContext.OnModelCreating (Fluent API).
    /// </summary>
    public Guid AppUserId { get => AuthorId; set => AuthorId = value; }
    
    [Range(1, 5)]
    public int Rating { get; set; }
    
    public LangStr? Comment { get; set; }
    
    public LangStr? ReviewType { get; set; } // Aircraft, Service
    
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsVerifiedBooking { get; set; } // Only from completed bookings
    
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
