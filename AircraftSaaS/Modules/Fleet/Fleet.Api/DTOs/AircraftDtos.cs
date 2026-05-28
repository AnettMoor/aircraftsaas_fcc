using System.ComponentModel.DataAnnotations;

namespace Fleet.Api.DTOs;

public class AircraftPhotoResponse
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public string Url { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class AircraftResponse
{
    public Guid Id { get; set; }
    public string RegistrationNumber { get; set; } = default!;
    public string Make { get; set; } = default!;
    public string Model { get; set; } = default!;
    public int Year { get; set; }
    public string Category { get; set; } = default!;
    public string RequiredLicenseType { get; set; } = "PPL";
    public int TotalAirspeedHours { get; set; }
    public decimal HourlyRate { get; set; }
    public Guid BaseAirportId { get; set; }
    public string BaseAirportName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public bool IsAvailable { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public List<string> PhotoUrls { get; set; } = new();
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsInsured { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public bool HasActiveMaintenance { get; set; }
    public string Status { get; set; } = "Available";
    public List<InsurancePolicyResponse> InsurancePolicies { get; set; } = new();
}

public class CreateAircraftRequest
{
    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string RegistrationNumber { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Make { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Model { get; set; } = default!;

    [Range(1900, 2100)]
    public int Year { get; set; }

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = default!;

    [Required]
    [StringLength(10)]
    public string RequiredLicenseType { get; set; } = "PPL";

    [Range(0, int.MaxValue)]
    public int TotalAirspeedHours { get; set; }

    [Range(0.01, 100_000)]
    public decimal HourlyRate { get; set; }

    [Required]
    public Guid BaseAirportId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = default!;

    public CreateInsurancePolicyRequest? InsurancePolicy { get; set; }
}

public class UpdateAircraftRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string RegistrationNumber { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Make { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Model { get; set; } = default!;

    [Range(1900, 2100)]
    public int Year { get; set; }

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = default!;

    [Required]
    [StringLength(10)]
    public string RequiredLicenseType { get; set; } = "PPL";

    [Range(0, int.MaxValue)]
    public int TotalAirspeedHours { get; set; }

    [Range(0.01, 100_000)]
    public decimal HourlyRate { get; set; }

    [Required]
    public Guid BaseAirportId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = default!;

    public bool IsAvailable { get; set; }

    public CreateInsurancePolicyRequest? InsurancePolicy { get; set; }
}

public class AircraftSearchRequest
{
    [StringLength(100)]
    public string? Make { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(50)]
    public string? Category { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [Range(0, 100_000)]
    public decimal? MaxHourlyRate { get; set; }

    [Range(1900, 2100)]
    public int? Year { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
