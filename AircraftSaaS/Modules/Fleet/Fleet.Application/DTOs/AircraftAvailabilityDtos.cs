namespace Fleet.Application.DTOs;

public class AircraftAvailabilityDto
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string AvailabilityType { get; set; } = default!;
    public string? Reason { get; set; }
}

public class CreateAircraftAvailabilityDto
{
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string AvailabilityType { get; set; } = default!;
    public string? Reason { get; set; }
}

public class UpdateAircraftAvailabilityDto
{
    public Guid Id { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string AvailabilityType { get; set; } = default!;
    public string? Reason { get; set; }
}
