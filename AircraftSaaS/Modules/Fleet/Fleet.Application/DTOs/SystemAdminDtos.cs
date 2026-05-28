using Shared.Contracts.Common;

namespace Fleet.Application.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Aircraft (system-wide)
// ─────────────────────────────────────────────────────────────────────────────

public class SystemAdminAircraftDto
{
    public Guid AircraftId { get; set; }
    public string RegistrationNumber { get; set; } = default!;
    public string Make { get; set; } = default!;
    public string Model { get; set; } = default!;
    public int Year { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsAvailable { get; set; }
    public string CompanyName { get; set; } = default!;
    public string BaseAirport { get; set; } = default!;
    public int TotalBookings { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AircraftListDto
{
    public PagedResult<SystemAdminAircraftDto> Aircraft { get; set; } = new();
    public IEnumerable<CompanySelectItemDto> Companies { get; set; } = new List<CompanySelectItemDto>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Airports
// ─────────────────────────────────────────────────────────────────────────────

public class SystemAdminAirportDto
{
    public Guid AirportId { get; set; }
    public string IcaoCode { get; set; } = default!;
    public string IataCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Elevation { get; set; }
    public int AircraftCount { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class AirportsListDto
{
    public PagedResult<SystemAdminAirportDto> Airports { get; set; } = new();
    public int DeletedAirports { get; set; }
}

public class AirportEditDto
{
    public Guid Id { get; set; }
    public string IcaoCode { get; set; } = default!;
    public string IataCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Elevation { get; set; }
}
