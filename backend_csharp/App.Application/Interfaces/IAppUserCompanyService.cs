using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IAppUserCompanyService
{
    Task<IEnumerable<AppUserCompanyDto>> GetAllAsync();
    Task<AppUserCompanyDto?> GetByIdAsync(Guid id);
    Task<AppUserCompanyDto> CreateAsync(CreateAppUserCompanyDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateAppUserCompanyDto dto);
    Task<bool> DeleteAsync(Guid id);
}
