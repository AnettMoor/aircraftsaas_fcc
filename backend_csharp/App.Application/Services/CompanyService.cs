using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain;
using Base.Domain;

namespace App.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IAppUOW _uow;
    
    public CompanyService(IAppUOW uow)
    {
        _uow = uow;
    }
    
    public async Task<CompanyDto?> GetByIdAsync(Guid id)
    {
        var company = await _uow.CompanyRepository.FindAsync(id);
        
        if (company == null)
            return null;
        
        return await MapToDtoAsync(company);
    }
    
    public async Task<CompanyDto?> GetBySlugAsync(string slug)
    {
        var company = await _uow.CompanyRepository.GetBySlugAsync(slug);
        
        if (company == null)
            return null;
        
        return await MapToDtoAsync(company);
    }
    
    public async Task<IEnumerable<CompanyDto>> GetAllAsync()
    {
        var companies = await _uow.CompanyRepository.GetAllActiveAsync();
        
        var dtos = new List<CompanyDto>();
        foreach (var company in companies)
        {
            dtos.Add(await MapToDtoAsync(company));
        }
        
        return dtos;
    }
    
    public async Task<CompanyDto> CreateAsync(CreateCompanyDto dto, string createdBy)
    {
        // Generate slug
        var slug = GenerateSlug(dto.CompanyName);
        
        // Check if slug exists
        var existingSlug = await _uow.CompanyRepository.ExistsBySlugAsync(slug);
        if (existingSlug)
        {
            slug = $"{slug}-{DateTime.UtcNow.Ticks % 10000}";
        }
        
        var company = new Company
        {
            CompanyName = new LangStr(dto.CompanyName),
            Slug = slug,
            IsActive = true,
            MaxUsers = 2,
            MaxAircraft = 3,
            MaxBookingsPerMonth = 20,
            Address = dto.Address ?? string.Empty,
            Phone = dto.Phone,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        
        _uow.CompanyRepository.Add(company);
        await _uow.SaveChangesAsync();
        
        return await MapToDtoAsync(company);
    }
    
    public async Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyDto dto, string updatedBy, Guid callerId, bool isAdmin = false)
    {
        // IDOR protection: only a company owner of this company or a system admin may update it
        if (!isAdmin)
        {
            var isOwner = await _uow.CompanyRepository.IsCompanyOwnerAsync(callerId, id);
            if (!isOwner)
                throw new UnauthorizedAccessException("Only company owners or system admins can update this company");
        }

        var company = await _uow.CompanyRepository.GetByIdTrackingAsync(id);
        
        if (company == null)
        {
            throw new InvalidOperationException("Company not found");
        }
        
        company.CompanyName.SetTranslation(dto.CompanyName);
        company.Address = dto.Address ?? string.Empty;
        company.Phone = dto.Phone;
        company.Email = dto.Email;
        company.UpdatedAt = DateTime.UtcNow;
        company.UpdatedBy = updatedBy;
        
        await _uow.SaveChangesAsync();
        
        return await MapToDtoAsync(company);
    }
    
    public async Task<bool> IsCompanyActiveAsync(Guid companyId)
    {
        var company = await _uow.CompanyRepository.FindAsync(companyId);
        return company?.IsActive ?? false;
    }
    
    public async Task DeleteAsync(Guid id, string deletedBy, Guid callerId, bool isAdmin = false)
    {
        // IDOR protection: only a system admin may delete a company
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only system admins can delete a company");
        }

        var company = await _uow.CompanyRepository.GetByIdIgnoreFiltersTrackingAsync(id);
        
        if (company == null)
        {
            throw new InvalidOperationException("Company not found");
        }
        
        company.SoftDelete(deletedBy);
        
        await _uow.SaveChangesAsync();
    }
    
    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "");
    }
    
    private async Task<CompanyDto> MapToDtoAsync(Company company)
    {
        var userCount = await _uow.CompanyRepository.GetUserCountAsync(company.Id);
        var aircraftCount = await _uow.CompanyRepository.GetAircraftCountAsync(company.Id);
        
        return new CompanyDto
        {
            Id = company.Id,
            CompanyName = company.CompanyName.ToString(),
            Slug = company.Slug,
            IsActive = company.IsActive,
            MaxUsers = company.MaxUsers,
            MaxAircraft = company.MaxAircraft,
            MaxBookingsPerMonth = company.MaxBookingsPerMonth,
            Address = company.Address,
            Phone = company.Phone,
            Email = company.Email,
            CurrentUserCount = userCount,
            CurrentAircraftCount = aircraftCount,
            CreatedAt = company.CreatedAt
        };
    }
}
