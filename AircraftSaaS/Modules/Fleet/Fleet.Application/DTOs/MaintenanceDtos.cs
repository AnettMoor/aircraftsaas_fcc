namespace Fleet.Application.DTOs;

public class MaintenanceRecordDto
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public string AircraftName { get; set; } = "";
    public DateTime MaintenanceDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string MaintenanceType { get; set; } = "";
    public string Status { get; set; } = "Scheduled";
    public string Description { get; set; } = "";
    public string PerformedBy { get; set; } = "";
    public int AirframeHoursAtMaintenance { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int? NextDueHours { get; set; }
    public decimal Cost { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMaintenanceRecordDto
{
    public Guid AircraftId { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string MaintenanceType { get; set; } = "";
    public string? Description { get; set; }
    public string? PerformedBy { get; set; }
    public int AirframeHoursAtMaintenance { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int? NextDueHours { get; set; }
    public decimal Cost { get; set; }
    public bool IsCompleted { get; set; }
}

public class UpdateMaintenanceRecordDto
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string MaintenanceType { get; set; } = "";
    public string? Description { get; set; }
    public string? PerformedBy { get; set; }
    public int AirframeHoursAtMaintenance { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int? NextDueHours { get; set; }
    public decimal Cost { get; set; }
    public bool IsCompleted { get; set; }
}
