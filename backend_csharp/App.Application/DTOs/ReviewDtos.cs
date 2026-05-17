namespace App.Application.DTOs;

public class ReviewDto
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

public class CreateReviewDto
{
    public Guid AircraftId { get; set; }
    public Guid BookingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewType { get; set; }
}

public class UpdateReviewDto
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewType { get; set; }
}
