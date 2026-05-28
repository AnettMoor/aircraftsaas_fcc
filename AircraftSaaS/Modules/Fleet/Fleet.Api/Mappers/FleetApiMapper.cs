using Fleet.Api.DTOs;
using Fleet.Application.DTOs;

namespace Fleet.Api.Mappers;

public static class FleetApiMapper
{
    // ── AircraftPhoto ─────────────────────────────────────────────────────────

    public static AircraftPhotoResponse ToResponse(this AircraftPhotoDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        Url = dto.ImageUrl,
        Description = dto.Description,
        IsPrimary = dto.IsPrimary,
        DisplayOrder = dto.DisplayOrder,
        UploadedAt = dto.UploadedAt,
    };

    public static IEnumerable<AircraftPhotoResponse> ToResponse(this IEnumerable<AircraftPhotoDto> dtos)
        => dtos.Select(d => d.ToResponse());

    // ── AircraftAvailability ──────────────────────────────────────────────────

    public static AircraftAvailabilityResponse ToResponse(this AircraftAvailabilityDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        StartDateTime = dto.StartDateTime,
        EndDateTime = dto.EndDateTime,
        AvailabilityType = dto.AvailabilityType,
        Reason = dto.Reason,
    };

    public static IEnumerable<AircraftAvailabilityResponse> ToResponse(this IEnumerable<AircraftAvailabilityDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateAircraftAvailabilityDto ToBllDto(this CreateAircraftAvailabilityRequest req) => new()
    {
        StartDateTime = req.StartDateTime,
        EndDateTime = req.EndDateTime,
        AvailabilityType = req.AvailabilityType,
        Reason = req.Reason,
    };

    public static UpdateAircraftAvailabilityDto ToBllDto(this UpdateAircraftAvailabilityRequest req) => new()
    {
        Id = req.Id,
        StartDateTime = req.StartDateTime,
        EndDateTime = req.EndDateTime,
        AvailabilityType = req.AvailabilityType,
        Reason = req.Reason,
    };

    // ── InsurancePolicy ─────────────────────────────────────────────────────

    public static InsurancePolicyResponse ToResponse(this InsurancePolicyDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        PolicyNumber = dto.PolicyNumber,
        InsuranceProvider = dto.InsuranceProvider,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        CoverageAmount = dto.CoverageAmount,
        CoverageType = dto.CoverageType,
        IsActive = dto.IsActive,
    };

    public static IEnumerable<InsurancePolicyResponse> ToResponse(this IEnumerable<InsurancePolicyDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateInsurancePolicyDto ToBllDto(this CreateInsurancePolicyRequest req) => new()
    {
        PolicyNumber = req.PolicyNumber,
        InsuranceProvider = req.InsuranceProvider,
        StartDate = req.StartDate,
        EndDate = req.EndDate,
        CoverageAmount = req.CoverageAmount,
        CoverageType = req.CoverageType,
    };

    public static UpdateInsurancePolicyDto ToBllDto(this UpdateInsurancePolicyRequest req) => new()
    {
        Id = req.Id,
        PolicyNumber = req.PolicyNumber,
        InsuranceProvider = req.InsuranceProvider,
        StartDate = req.StartDate,
        EndDate = req.EndDate,
        CoverageAmount = req.CoverageAmount,
        CoverageType = req.CoverageType,
    };

    // ── Aircraft ──────────────────────────────────────────────────────────────

    public static AircraftResponse ToResponse(this AircraftDto dto) => new()
    {
        Id = dto.Id,
        RegistrationNumber = dto.RegistrationNumber,
        Make = dto.Make,
        Model = dto.Model,
        Year = dto.Year,
        Category = dto.Category,
        RequiredLicenseType = dto.RequiredLicenseType,
        TotalAirspeedHours = dto.TotalAirspeedHours,
        HourlyRate = dto.HourlyRate,
        BaseAirportId = dto.BaseAirportId,
        BaseAirportName = dto.BaseAirportName,
        Description = dto.Description,
        IsAvailable = dto.IsAvailable,
        CompanyId = dto.CompanyId,
        CompanyName = dto.CompanyName,
        PhotoUrls = dto.PhotoUrls,
        AverageRating = 0,
        ReviewCount = 0,
        IsInsured = dto.IsInsured,
        InsuranceExpiryDate = dto.InsuranceExpiryDate,
        HasActiveMaintenance = dto.HasActiveMaintenance,
        Status = dto.Status,
        InsurancePolicies = dto.InsurancePolicies.Select(p => p.ToResponse()).ToList(),
    };

    public static IEnumerable<AircraftResponse> ToResponse(this IEnumerable<AircraftDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateAircraftDto ToBllDto(this CreateAircraftRequest req) => new()
    {
        RegistrationNumber = req.RegistrationNumber,
        Make = req.Make,
        Model = req.Model,
        Year = req.Year,
        Category = req.Category,
        RequiredLicenseType = req.RequiredLicenseType,
        TotalAirspeedHours = req.TotalAirspeedHours,
        HourlyRate = req.HourlyRate,
        BaseAirportId = req.BaseAirportId,
        Description = req.Description,
        InsurancePolicy = req.InsurancePolicy?.ToBllDto(),
    };

    public static UpdateAircraftDto ToBllDto(this UpdateAircraftRequest req) => new()
    {
        Id = req.Id,
        RegistrationNumber = req.RegistrationNumber,
        Make = req.Make,
        Model = req.Model,
        Year = req.Year,
        Category = req.Category,
        RequiredLicenseType = req.RequiredLicenseType,
        TotalAirspeedHours = req.TotalAirspeedHours,
        HourlyRate = req.HourlyRate,
        BaseAirportId = req.BaseAirportId,
        Description = req.Description,
        IsAvailable = req.IsAvailable,
        InsurancePolicy = req.InsurancePolicy?.ToBllDto(),
    };

    public static AircraftSearchDto ToBllDto(this AircraftSearchRequest req) => new()
    {
        Make = req.Make,
        Model = req.Model,
        Category = req.Category,
        Location = req.Location,
        Status = req.Status,
        StartDate = req.StartDate,
        EndDate = req.EndDate,
        MaxHourlyRate = req.MaxHourlyRate,
        Year = req.Year,
        Page = req.Page,
        PageSize = req.PageSize,
    };

    // ── Airport ───────────────────────────────────────────────────────────────

    public static AirportResponse ToResponse(this AirportDto dto) => new()
    {
        Id = dto.Id,
        IcaoCode = dto.IcaoCode,
        IataCode = dto.IataCode,
        Name = dto.Name,
        City = dto.City,
        Country = dto.Country,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        Elevation = dto.Elevation,
    };

    public static IEnumerable<AirportResponse> ToResponse(this IEnumerable<AirportDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateAirportDto ToBllDto(this CreateAirportRequest req) => new()
    {
        IcaoCode = req.IcaoCode,
        IataCode = req.IataCode,
        Name = req.Name,
        City = req.City,
        Country = req.Country,
        Latitude = req.Latitude,
        Longitude = req.Longitude,
        Elevation = req.Elevation,
    };

    public static UpdateAirportDto ToBllDto(this UpdateAirportRequest req) => new()
    {
        Id = req.Id,
        IcaoCode = req.IcaoCode,
        IataCode = req.IataCode,
        Name = req.Name,
        City = req.City,
        Country = req.Country,
        Latitude = req.Latitude,
        Longitude = req.Longitude,
        Elevation = req.Elevation,
    };

    // ── Maintenance ───────────────────────────────────────────────────────────

    public static MaintenanceRecordResponse ToResponse(this MaintenanceRecordDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        AircraftName = dto.AircraftName,
        MaintenanceDate = dto.MaintenanceDate,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        MaintenanceType = dto.MaintenanceType,
        Status = dto.Status,
        Description = dto.Description,
        PerformedBy = dto.PerformedBy,
        AirframeHoursAtMaintenance = dto.AirframeHoursAtMaintenance,
        NextDueDate = dto.NextDueDate,
        NextDueHours = dto.NextDueHours,
        Cost = dto.Cost,
        IsCompleted = dto.IsCompleted,
        CreatedAt = dto.CreatedAt,
    };

    public static IEnumerable<MaintenanceRecordResponse> ToResponse(this IEnumerable<MaintenanceRecordDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateMaintenanceRecordDto ToBllDto(this CreateMaintenanceRecordRequest req) => new()
    {
        AircraftId = req.AircraftId,
        MaintenanceDate = req.MaintenanceDate,
        StartDate = req.StartDate,
        EndDate = req.EndDate,
        MaintenanceType = req.MaintenanceType,
        Description = req.Description,
        PerformedBy = req.PerformedBy,
        AirframeHoursAtMaintenance = req.AirframeHoursAtMaintenance,
        NextDueDate = req.NextDueDate,
        NextDueHours = req.NextDueHours,
        Cost = req.Cost,
        IsCompleted = req.IsCompleted,
    };

    public static UpdateMaintenanceRecordDto ToBllDto(this UpdateMaintenanceRecordRequest req) => new()
    {
        Id = req.Id,
        AircraftId = req.AircraftId,
        MaintenanceDate = req.MaintenanceDate,
        StartDate = req.StartDate,
        EndDate = req.EndDate,
        MaintenanceType = req.MaintenanceType,
        Description = req.Description,
        PerformedBy = req.PerformedBy,
        AirframeHoursAtMaintenance = req.AirframeHoursAtMaintenance,
        NextDueDate = req.NextDueDate,
        NextDueHours = req.NextDueHours,
        Cost = req.Cost,
        IsCompleted = req.IsCompleted,
    };
}
