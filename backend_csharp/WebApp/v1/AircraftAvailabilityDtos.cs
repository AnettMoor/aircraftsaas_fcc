using System.ComponentModel.DataAnnotations;

namespace WebApp.v1;

public class AircraftAvailabilityResponse
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string AvailabilityType { get; set; } = default!;
    public string? Reason { get; set; }
}

public class CreateAircraftAvailabilityRequest
{
    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    [RegularExpression("Available|Blocked|Maintenance",
        ErrorMessage = "AvailabilityType must be Available, Blocked, or Maintenance.")]
    public string AvailabilityType { get; set; } = default!;

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class UpdateAircraftAvailabilityRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    [RegularExpression("Available|Blocked|Maintenance",
        ErrorMessage = "AvailabilityType must be Available, Blocked, or Maintenance.")]
    public string AvailabilityType { get; set; } = default!;

    [StringLength(500)]
    public string? Reason { get; set; }
}
