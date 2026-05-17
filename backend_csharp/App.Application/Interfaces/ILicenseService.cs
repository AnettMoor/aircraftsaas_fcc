using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface ILicenseService
{
    Task<LicenseDto?> GetByIdAsync(Guid id, Guid userId);
    Task<IEnumerable<LicenseDto>> GetAllForUserAsync(Guid userId);
    Task<LicenseDto> CreateAsync(CreateLicenseDto dto, Guid userId);
    Task<LicenseDto> UpdateAsync(Guid id, UpdateLicenseDto dto, Guid userId);
    Task DeleteAsync(Guid id, Guid userId, string deletedBy);
}
