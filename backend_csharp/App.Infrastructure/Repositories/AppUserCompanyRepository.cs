using App.Domain.Contracts;
using App.Domain;
using App.Infrastructure.Mappers;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class AppUserCompanyRepository : BaseRepository<AppUserCompany, AppUserCompany, AppDbContext>, IAppUserCompanyRepository
{
    public AppUserCompanyRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<AppUserCompany>())
    {
    }

    public async Task<AppUserCompany?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(auc => auc.Id == id);
    }

    // System-admin methods
    
    public async Task<IEnumerable<AppUserCompany>> GetAllForUserWithCompanyAsync(Guid userId)
    {
        return await RepositoryDbSet
            .Include(uc => uc.Company)
            .Where(uc => uc.AppUserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<AppUserCompany>> GetAllForUserTrackingAsync(Guid userId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Where(uc => uc.AppUserId == userId)
            .ToListAsync();
    }

    public async Task<(string? Name, string? Email)> GetCompanyOwnerInfoAsync(Guid companyId)
    {
        var ownerUser = await RepositoryDbSet
            .Where(uc => uc.CompanyId == companyId && uc.AppUserRoleInCompany == EAppUserRoleInCompany.CompanyOwner)
            .Join(RepositoryDbContext.Users, uc => uc.AppUserId, u => u.Id, (uc, u) => u)
            .FirstOrDefaultAsync();

        if (ownerUser == null)
            return (null, null);

        var name = $"{ownerUser.FirstName} {ownerUser.LastName}".Trim();
        return (string.IsNullOrWhiteSpace(name) ? null : name, ownerUser.Email);
    }

    public async Task<IEnumerable<string>> GetCompanyNamesForUserAsync(Guid userId)
    {
        var companyNames = await RepositoryDbSet
            .Where(uc => uc.AppUserId == userId)
            .Join(RepositoryDbContext.Companies, uc => uc.CompanyId, c => c.Id, (uc, c) => c.CompanyName)
            .ToListAsync();
        
        return companyNames.Select(cn => cn.ToString()).ToList();
    }

    public void RemoveRange(IEnumerable<AppUserCompany> entities)
    {
        RepositoryDbSet.RemoveRange(entities);
    }
}
