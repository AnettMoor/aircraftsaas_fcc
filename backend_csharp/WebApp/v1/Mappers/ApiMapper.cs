using App.Application.DTOs;
using App.Domain.DTOs;

namespace WebApp.v1.Mappers;

public static class ApiMapper
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

    // ── License ───────────────────────────────────────────────────────────────

    public static LicenseResponse ToResponse(this LicenseDto dto) => new()
    {
        Id = dto.Id,
        AppUserId = dto.AppUserId,
        LicenseNumber = dto.LicenseNumber,
        LicenseType = dto.LicenseType,
        IssueDate = dto.IssueDate,
        ExpiryDate = dto.ExpiryDate,
        IssuingAuthority = dto.IssuingAuthority,
        IsValid = dto.IsValid,
    };

    public static IEnumerable<LicenseResponse> ToResponse(this IEnumerable<LicenseDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateLicenseDto ToBllDto(this CreateLicenseRequest req) => new()
    {
        LicenseNumber = req.LicenseNumber,
        LicenseType = req.LicenseType,
        IssueDate = req.IssueDate,
        ExpiryDate = req.ExpiryDate,
        IssuingAuthority = req.IssuingAuthority,
    };

    public static UpdateLicenseDto ToBllDto(this UpdateLicenseRequest req) => new()
    {
        Id = req.Id,
        LicenseNumber = req.LicenseNumber,
        LicenseType = req.LicenseType,
        IssueDate = req.IssueDate,
        ExpiryDate = req.ExpiryDate,
        IssuingAuthority = req.IssuingAuthority,
    };

    // ── Aircraft ──────────────────────────────────────────────────────────────
    // .ToBllDto() — Converts a public Request → BLL DTO (inbound)
    // .ToResponse() — Converts a BLL DTO → public Response (outbound)

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
        AverageRating = dto.AverageRating,
        ReviewCount = dto.ReviewCount,
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

    // ── Booking ───────────────────────────────────────────────────────────────

    public static BookingResponse ToResponse(this BookingDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        AircraftName = dto.AircraftName,
        PilotId = dto.PilotId,
        PilotName = dto.PilotName,
        StartDateTime = dto.StartDateTime,
        EndDateTime = dto.EndDateTime,
        Status = dto.Status,
        Purpose = dto.Purpose,
        TotalAmount = dto.TotalAmount,
        RejectionReason = dto.RejectionReason,
        ApprovedAt = dto.ApprovedAt,
        PaidAt = dto.PaidAt,
        CompletedAt = dto.CompletedAt,
        CancelledAt = dto.CancelledAt,
        CompanyId = dto.CompanyId,
        CreatedAt = dto.CreatedAt,
    };

    public static IEnumerable<BookingResponse> ToResponse(this IEnumerable<BookingDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateBookingDto ToBllDto(this CreateBookingRequest req) => new()
    {
        AircraftId = req.AircraftId,
        StartDateTime = req.StartDateTime,
        EndDateTime = req.EndDateTime,
        Purpose = req.Purpose,
    };

    public static UpdateBookingDto ToBllDto(this UpdateBookingRequest req) => new()
    {
        Id = req.Id,
        StartDateTime = req.StartDateTime,
        EndDateTime = req.EndDateTime,
        Purpose = req.Purpose,
    };

    public static PaymentDto ToBllDto(this PaymentRequest req) => new()
    {
        PaymentMethod = req.PaymentMethod,
        TransactionId = req.TransactionId,
        PaymentDetails = req.PaymentDetails,
    };

    // ── Company ───────────────────────────────────────────────────────────────

    public static CompanyResponse ToResponse(this CompanyDto dto) => new()
    {
        Id = dto.Id,
        CompanyName = dto.CompanyName,
        Slug = dto.Slug,
        IsActive = dto.IsActive,
        MaxUsers = dto.MaxUsers,
        MaxAircraft = dto.MaxAircraft,
        MaxBookingsPerMonth = dto.MaxBookingsPerMonth,
        Address = dto.Address,
        Phone = dto.Phone,
        Email = dto.Email,
        CurrentUserCount = dto.CurrentUserCount,
        CurrentAircraftCount = dto.CurrentAircraftCount,
        CreatedAt = dto.CreatedAt,
    };

    public static IEnumerable<CompanyResponse> ToResponse(this IEnumerable<CompanyDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateCompanyDto ToBllDto(this CreateCompanyRequest req) => new()
    {
        CompanyName = req.CompanyName,
        Address = req.Address,
        Phone = req.Phone,
        Email = req.Email,
    };

    public static UpdateCompanyDto ToBllDto(this UpdateCompanyRequest req) => new()
    {
        CompanyName = req.CompanyName,
        Address = req.Address,
        Phone = req.Phone,
        Email = req.Email,
    };

    // ── Review ────────────────────────────────────────────────────────────────

    public static ReviewResponse ToResponse(this ReviewDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        AircraftName = dto.AircraftName,
        BookingId = dto.BookingId,
        AuthorId = dto.AuthorId,
        AuthorName = dto.AuthorName,
        Rating = dto.Rating,
        Comment = dto.Comment,
        ReviewType = dto.ReviewType,
        ReviewedAt = dto.ReviewedAt,
        IsVerifiedBooking = dto.IsVerifiedBooking,
    };

    public static IEnumerable<ReviewResponse> ToResponse(this IEnumerable<ReviewDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateReviewDto ToBllDto(this CreateReviewRequest req) => new()
    {
        AircraftId = req.AircraftId,
        BookingId = req.BookingId,
        Rating = req.Rating,
        Comment = req.Comment,
        ReviewType = req.ReviewType,
    };

    public static UpdateReviewDto ToBllDto(this UpdateReviewRequest req) => new()
    {
        Id = req.Id,
        Rating = req.Rating,
        Comment = req.Comment,
        ReviewType = req.ReviewType,
    };

    // ── AuditLog ──────────────────────────────────────────────────────────────

    public static AuditLogResponse ToResponse(this AuditLogDto dto) => new()
    {
        Id = dto.Id,
        TenantId = dto.TenantId,
        UserId = dto.UserId,
        UserName = dto.UserName,
        EntityName = dto.EntityName,
        EntityId = dto.EntityId,
        Action = dto.Action,
        OldValues = dto.OldValues,
        NewValues = dto.NewValues,
        Timestamp = dto.Timestamp,
        IpAddress = dto.IpAddress,
        Details = dto.Details,
    };

    public static IEnumerable<AuditLogResponse> ToResponse(this IEnumerable<AuditLogDto> dtos)
        => dtos.Select(d => d.ToResponse());

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
