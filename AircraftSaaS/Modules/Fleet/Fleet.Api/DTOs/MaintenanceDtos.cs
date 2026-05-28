using System.ComponentModel.DataAnnotations;

namespace Fleet.Api.DTOs;

public class MaintenanceRecordResponse
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

public class CreateMaintenanceRecordRequest
{
    public Guid Id { get; set; }

    [Required]
    public Guid AircraftId { get; set; }

    [Required]
    public DateTime MaintenanceDate { get; set; }

    /// <summary>Start of the maintenance timeframe (blocks aircraft availability)</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>End of the maintenance timeframe (blocks aircraft availability)</summary>
    public DateTime? EndDate { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string MaintenanceType { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? PerformedBy { get; set; }

    [Range(0, int.MaxValue)]
    public int AirframeHoursAtMaintenance { get; set; }

    public DateTime? NextDueDate { get; set; }

    [Range(0, int.MaxValue)]
    public int? NextDueHours { get; set; }

    [Range(0, 10_000_000)]
    public decimal Cost { get; set; }

    public bool IsCompleted { get; set; }
}

public class UpdateMaintenanceRecordRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AircraftId { get; set; }

    [Required]
    public DateTime MaintenanceDate { get; set; }

    /// <summary>Start of the maintenance timeframe (blocks aircraft availability)</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>End of the maintenance timeframe (blocks aircraft availability)</summary>
    public DateTime? EndDate { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string MaintenanceType { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? PerformedBy { get; set; }

    [Range(0, int.MaxValue)]
    public int AirframeHoursAtMaintenance { get; set; }

    public DateTime? NextDueDate { get; set; }

    [Range(0, int.MaxValue)]
    public int? NextDueHours { get; set; }

    [Range(0, 10_000_000)]
    public decimal Cost { get; set; }

    public bool IsCompleted { get; set; }
}
