using Shared.Kernel.Domain;
using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Domain.Entities;

namespace Users.Application.Services;

internal sealed class LicenseService : ILicenseService
{
    private readonly IUsersUOW _uow;

    public LicenseService(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<LicenseDto?> GetByIdAsync(Guid id, Guid userId)
    {
        var license = await _uow.LicenseRepository.GetByIdForUserAsync(id, userId);
        return license == null ? null : MapToDto(license);
    }

    public async Task<IEnumerable<LicenseDto>> GetAllForUserAsync(Guid userId)
    {
        var licenses = await _uow.LicenseRepository.GetAllForUserAsync(userId);
        return licenses.Select(MapToDto);
    }

    public async Task<LicenseDto> CreateAsync(CreateLicenseDto dto, Guid userId)
    {
        var license = new License
        {
            AppUserId = userId,
            LicenseNumber = dto.LicenseNumber,
            LicenseType = new LangStr(dto.LicenseType),
            IssueDate = DateTime.SpecifyKind(dto.IssueDate, DateTimeKind.Utc),
            ExpiryDate = DateTime.SpecifyKind(dto.ExpiryDate, DateTimeKind.Utc),
            IssuingAuthority = new LangStr(dto.IssuingAuthority)
        };

        _uow.LicenseRepository.Add(license);
        await _uow.SaveChangesAsync();

        return MapToDto(license);
    }

    public async Task<LicenseDto> UpdateAsync(Guid id, UpdateLicenseDto dto, Guid userId)
    {
        var license = await _uow.LicenseRepository.GetByIdTrackingAsync(id);
        if (license == null)
            throw new InvalidOperationException("License not found.");

        if (license.AppUserId != userId)
            throw new UnauthorizedAccessException("You can only update your own licenses.");

        license.LicenseNumber = dto.LicenseNumber;
        license.LicenseType.SetTranslation(dto.LicenseType);
        license.IssueDate = DateTime.SpecifyKind(dto.IssueDate, DateTimeKind.Utc);
        license.ExpiryDate = DateTime.SpecifyKind(dto.ExpiryDate, DateTimeKind.Utc);
        license.IssuingAuthority.SetTranslation(dto.IssuingAuthority);

        await _uow.SaveChangesAsync();

        return MapToDto(license);
    }

    public async Task DeleteAsync(Guid id, Guid userId, string deletedBy)
    {
        var license = await _uow.LicenseRepository.GetByIdTrackingAsync(id);
        if (license == null)
            throw new InvalidOperationException("License not found.");

        if (license.AppUserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own licenses.");

        license.SoftDelete(deletedBy);
        await _uow.SaveChangesAsync();
    }

    private static LicenseDto MapToDto(License license) => new()
    {
        Id = license.Id,
        AppUserId = license.AppUserId,
        LicenseNumber = license.LicenseNumber,
        LicenseType = license.LicenseType.ToString(),
        IssueDate = license.IssueDate,
        ExpiryDate = license.ExpiryDate,
        IssuingAuthority = license.IssuingAuthority.ToString(),
        IsValid = license.IsValid
    };
}
