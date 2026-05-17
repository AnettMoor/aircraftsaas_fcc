using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class LicenseRepository : BaseRepository<License, License, AppDbContext>, ILicenseRepository
{
    public LicenseRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<License>())
    {
    }

    public async Task<IEnumerable<License>> GetAllForUserAsync(Guid userId)
    {
        return await RepositoryDbSet
            .Where(l => l.AppUserId == userId && l.DeletedAt == null)
            .OrderByDescending(l => l.ExpiryDate)
            .ToListAsync();
    }

    public async Task<License?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        return await RepositoryDbSet
            .FirstOrDefaultAsync(l => l.Id == id && l.AppUserId == userId && l.DeletedAt == null);
    }

    public async Task<License?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == id && l.DeletedAt == null);
    }

    public async Task<IEnumerable<License>> GetValidLicensesForUserAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        return await RepositoryDbSet
            .Where(l => l.AppUserId == userId && l.DeletedAt == null && l.ExpiryDate > now)
            .OrderByDescending(l => l.ExpiryDate)
            .ToListAsync();
    }

    public async Task<bool> HasValidLicenseForTypeAsync(Guid userId, string licenseType, DateTime asOfDate)
    {
        return await RepositoryDbSet
            .AnyAsync(l => l.AppUserId == userId
                           && l.DeletedAt == null
                           && l.LicenseType.ToString().Contains(licenseType)
                           && l.IssueDate <= asOfDate
                           && l.ExpiryDate > asOfDate);
    }
}
