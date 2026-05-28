using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Domain.Entities;
using Users.Domain.Enums;

namespace Users.Application.Services;

internal sealed class AppUserCompanyService : IAppUserCompanyService
{
    private readonly IUsersUOW _uow;

    public AppUserCompanyService(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<AppUserCompanyDto>> GetAllAsync()
    {
        var items = await _uow.AppUserCompanyRepository.AllAsync();
        return items.Select(MapToDto);
    }

    public async Task<AppUserCompanyDto?> GetByIdAsync(Guid id)
    {
        var item = await _uow.AppUserCompanyRepository.FindAsync(id);
        return item == null ? null : MapToDto(item);
    }

    public async Task<AppUserCompanyDto> CreateAsync(CreateAppUserCompanyDto dto)
    {
        if (dto.AppUserRoleInCompany == EAppUserRoleInCompany.Normal)
        {
            throw new InvalidOperationException("Normal users cannot be associated with a company.");
        }

        var entity = new AppUserCompany
        {
            AppUserId = dto.AppUserId,
            CompanyId = dto.CompanyId,
            AppUserRoleInCompany = dto.AppUserRoleInCompany,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        };

        _uow.AppUserCompanyRepository.Add(entity);
        await _uow.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAppUserCompanyDto dto)
    {
        var entity = await _uow.AppUserCompanyRepository.GetByIdTrackingAsync(id);
        if (entity == null) return false;

        // Only allow updating role and active status
        entity.AppUserRoleInCompany = dto.AppUserRoleInCompany;
        entity.IsActive = dto.IsActive;
        entity.UpdatedBy = "system";
        entity.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _uow.AppUserCompanyRepository.FindAsync(id);
        if (entity == null) return false;

        _uow.AppUserCompanyRepository.Remove(entity);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static AppUserCompanyDto MapToDto(AppUserCompany entity)
    {
        return new AppUserCompanyDto
        {
            Id = entity.Id,
            AppUserId = entity.AppUserId,
            CompanyId = entity.CompanyId,
            AppUserRoleInCompany = entity.AppUserRoleInCompany,
            IsActive = entity.IsActive,
            JoinedAt = entity.JoinedAt
        };
    }
}
