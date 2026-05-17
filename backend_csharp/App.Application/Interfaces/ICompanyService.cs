using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface ICompanyService
{
    Task<CompanyDto?> GetByIdAsync(Guid id);
    Task<CompanyDto?> GetBySlugAsync(string slug);
    Task<IEnumerable<CompanyDto>> GetAllAsync();
    Task<CompanyDto> CreateAsync(CreateCompanyDto dto, string createdBy);
    Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyDto dto, string updatedBy, Guid callerId, bool isAdmin = false);
    Task DeleteAsync(Guid id, string deletedBy, Guid callerId, bool isAdmin = false);
    Task<bool> IsCompanyActiveAsync(Guid companyId);
}
