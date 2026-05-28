using Users.Application.DTOs;
using Users.Api.DTOs;

namespace Users.Api.Mappers;

public static class UsersApiMapper
{
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
}
