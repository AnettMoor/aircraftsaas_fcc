using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class CompanyRepository : BaseRepository<Company, Company, AppDbContext>, ICompanyRepository
{
    public CompanyRepository(AppDbContext dbContext, IBaseMapper<Company, Company> mapper)
        : base(dbContext, mapper)
    {
    }

    public CompanyRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<Company>())
    {
    }

    public async Task<Company?> GetBySlugAsync(string slug)
    {
        return await RepositoryDbSet.FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);
    }

    public async Task<IEnumerable<Company>> GetAllActiveAsync()
    {
        var companies = await RepositoryDbSet.Where(c => c.IsActive).ToListAsync();
        return companies.OrderBy(c => c.CompanyName.ToString());
    }

    public async Task<bool> ExistsBySlugAsync(string slug)
    {
        return await RepositoryDbSet.AnyAsync(c => c.Slug == slug);
    }

    public async Task<Company?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Company?> GetByIdIgnoreFiltersTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int> GetUserCountAsync(Guid companyId)
    {
        return await RepositoryDbContext.AppUserCompanies.CountAsync(uc => uc.CompanyId == companyId);
    }

    public async Task<int> GetAircraftCountAsync(Guid companyId)
    {
        return await RepositoryDbContext.Aircraft.CountAsync(a => a.CompanyId == companyId);
    }

    public async Task<bool> IsCompanyOwnerAsync(Guid userId, Guid companyId)
    {
        return await RepositoryDbContext.AppUserCompanies
            .AnyAsync(uc => uc.AppUserId == userId &&
                            uc.CompanyId == companyId &&
                            uc.IsActive &&
                            uc.AppUserRoleInCompany == EAppUserRoleInCompany.CompanyOwner);
    }

    public async Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId)
    {
        return await RepositoryDbContext.AppUserCompanies
            .AnyAsync(uc => uc.AppUserId == userId &&
                            uc.CompanyId == companyId &&
                            uc.IsActive);
    }

    public async Task<EAppUserRoleInCompany?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId)
    {
        var membership = await RepositoryDbContext.AppUserCompanies
            .FirstOrDefaultAsync(uc => uc.AppUserId == userId &&
                                       uc.CompanyId == companyId &&
                                       uc.IsActive);
        return membership?.Role;
    }

    public async Task<IEnumerable<AppUserCompany>> GetUserCompaniesAsync(Guid userId)
    {
        var results = await RepositoryDbContext.AppUserCompanies
            .Include(uc => uc.Company)
            .Where(uc => uc.AppUserId == userId && uc.IsActive)
            .ToListAsync();
        // Sort in memory by CompanyName (LangStr/jsonb cannot be ordered in SQL directly)
        return results.OrderBy(uc => uc.Company?.CompanyName.ToString()).ToList();
    }

    // System-admin methods
    
    public async Task<IEnumerable<Company>> GetAllNonDeletedAsync()
    {
        return await RepositoryDbSet
            .Where(c => c.DeletedAt == null)
            .ToListAsync();
    }

    public async Task<Company?> GetByIdNonDeletedTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);
    }

    public async Task<bool> ExistsBySlugIgnoreFiltersAsync(string slug)
    {
        return await RepositoryDbSet
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Slug == slug);
    }

    public async Task<bool> ExistsByIdNonDeletedAsync(Guid id)
    {
        return await RepositoryDbSet
            .AnyAsync(c => c.Id == id && c.DeletedAt == null);
    }
}
