using System.ComponentModel.DataAnnotations;

namespace Booking.Api.DTOs;

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public string AircraftName { get; set; } = default!;
    public Guid BookingId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = default!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewType { get; set; }
    public DateTime ReviewedAt { get; set; }
    public bool IsVerifiedBooking { get; set; }
}

public class CreateReviewRequest
{
    [Required]
    public Guid AircraftId { get; set; }

    [Required]
    public Guid BookingId { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }

    [StringLength(50)]
    public string? ReviewType { get; set; }
}

public class UpdateReviewRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }

    [StringLength(50)]
    public string? ReviewType { get; set; }
}
