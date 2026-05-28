namespace Shared.Contracts.Fleet.DTOs;

public record AircraftBasicDto(
    Guid Id,
    string Registration,
    string Model,
    Guid CompanyId,
    string? RequiredLicenseType);
