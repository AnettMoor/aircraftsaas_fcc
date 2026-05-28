namespace Fleet.Application.DTOs;

public class AircraftDto
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
    // Cross-module: populated via IMediator when needed
    public string CompanyName { get; set; } = default!;
    public string? CompanyEmail { get; set; }
    public string? CompanyPhone { get; set; }
    public List<string> PhotoUrls { get; set; } = new();
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsInsured { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public bool HasActiveMaintenance { get; set; }
    public string Status { get; set; } = "Available"; // Available, Unavailable, InsuranceInactive, Maintenance
    public List<InsurancePolicyDto> InsurancePolicies { get; set; } = new();
}

public class CreateAircraftDto
{
    public string RegistrationNumber { get; set; } = default!;
    public string Make { get; set; } = default!;
    public string Model { get; set; } = default!;
    public int Year { get; set; }
    public string Category { get; set; } = default!;
    public string RequiredLicenseType { get; set; } = "PPL";
    public int TotalAirspeedHours { get; set; }
    public decimal HourlyRate { get; set; }
    public Guid BaseAirportId { get; set; }
    public string Description { get; set; } = default!;
    public CreateInsurancePolicyDto? InsurancePolicy { get; set; }
}

public class UpdateAircraftDto
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
    public string Description { get; set; } = default!;
    public bool IsAvailable { get; set; }
    public CreateInsurancePolicyDto? InsurancePolicy { get; set; }
}

public class AircraftSearchDto
{
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MaxHourlyRate { get; set; }
    public int? Year { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AircraftPhotoDto
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public string ImageUrl { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class AddAircraftPhotoDto
{
    public string ImageUrl { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}
