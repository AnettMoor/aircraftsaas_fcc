using System.ComponentModel.DataAnnotations;
using App.Domain;
using App.Domain.Identity;
using Base.Contracts.Domain;
using Base.Domain;

namespace App.Domain.Entities;

public class Review : BaseEntity, ISoftDelete, IAppUserId<Guid>
{
    public Guid AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }
    
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    
    public Guid AuthorId { get; set; }
    public AppUser? Author { get; set; }
    
    /// <summary>
    /// Maps to AuthorId for BaseRepository IDOR filtering.
    /// Configured as Ignored in AppDbContext.OnModelCreating (Fluent API).
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
