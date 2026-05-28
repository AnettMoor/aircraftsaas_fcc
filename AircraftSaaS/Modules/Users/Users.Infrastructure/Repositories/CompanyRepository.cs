using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;
using Shared.Contracts.Users.DTOs;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;
using Users.Domain.Enums;

namespace Users.Infrastructure.Repositories;

internal sealed class CompanyRepository : BaseRepository<Company, Company, UsersDbContext>, ICompanyRepository
{
    public CompanyRepository(UsersDbContext dbContext, IBaseMapper<Company, Company> mapper)
        : base(dbContext, mapper)
    {
    }

    public CompanyRepository(UsersDbContext dbContext)
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

    public async Task<CompanyBasicDto?> GetBasicByIdAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await RepositoryDbSet
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        if (company == null)
            return null;

        return new CompanyBasicDto(company.Id, company.CompanyName.ToString());
    }

    public async Task<Dictionary<Guid, CompanyBasicDto>> GetBasicsByIdsAsync(IEnumerable<Guid> companyIds, CancellationToken ct = default)
    {
        var idList = companyIds.ToList();
        var companies = await RepositoryDbSet
            .Where(c => idList.Contains(c.Id))
            .AsNoTracking()
            .ToListAsync(ct);

        return companies.ToDictionary(
            c => c.Id,
            c => new CompanyBasicDto(c.Id, c.CompanyName.ToString()));
    }

    public async Task<List<CompanySelectItemDto>> GetActiveSelectItemsAsync(CancellationToken ct = default)
    {
        var companies = await RepositoryDbSet
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.CompanyName)
            .ToListAsync(ct);

        return companies.Select(c => new CompanySelectItemDto
        {
            Id = c.Id,
            CompanyName = c.CompanyName.ToString()
        }).ToList();
    }

    public async Task<UserBasicDto?> GetUserBasicByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RepositoryDbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return null;

        return new UserBasicDto(user.Id, user.Email ?? string.Empty, user.FirstName ?? string.Empty, user.LastName ?? string.Empty);
    }

    public async Task<Dictionary<Guid, UserBasicDto>> GetUserBasicsByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var idList = userIds.ToList();
        var users = await RepositoryDbContext.Users
            .Where(u => idList.Contains(u.Id))
            .AsNoTracking()
            .ToListAsync(ct);

        return users.ToDictionary(
            u => u.Id,
            u => new UserBasicDto(u.Id, u.Email ?? string.Empty, u.FirstName ?? string.Empty, u.LastName ?? string.Empty));
    }

    public async Task<List<UserBasicDto>> GetCompanyUserBasicsAsync(Guid companyId, CancellationToken ct = default)
    {
        var userIds = await RepositoryDbContext.AppUserCompanies
            .Where(uc => uc.CompanyId == companyId && uc.IsActive)
            .Select(uc => uc.AppUserId)
            .Distinct()
            .ToListAsync(ct);

        return await RepositoryDbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserBasicDto(u.Id, u.Email ?? string.Empty, u.FirstName ?? string.Empty, u.LastName ?? string.Empty))
            .ToListAsync(ct);
    }
}
