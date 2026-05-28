using Microsoft.EntityFrameworkCore;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;

namespace Users.Infrastructure.Repositories;

internal sealed class LicenseRepository : BaseRepository<License, License, UsersDbContext>, ILicenseRepository
{
    public LicenseRepository(UsersDbContext dbContext)
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
        // License.LicenseType is a LangStr (Dictionary<string,string> persisted as jsonb).
        // EF Core cannot translate LangStr.ToString() to SQL, so the original
        //     l.LicenseType.ToString().Contains(licenseType)
        // predicate threw InvalidOperationException at query-compile time, which
        // bubbled up as a 500 to the Booking service and was misreported as
        // "You must have a valid pilot license for this aircraft type before you can book."
        //
        // Solution: filter date+user in SQL (translatable) and do the licence-type
        // match in memory using LangStr's implicit string operator.

        var candidates = await RepositoryDbSet
            .Where(l => l.AppUserId == userId
                        && l.DeletedAt == null
                        && l.IssueDate <= asOfDate
                        && l.ExpiryDate > asOfDate)
            .ToListAsync();

        if (candidates.Count == 0) return false;

        var needle = (licenseType ?? string.Empty).Trim();
        if (needle.Length == 0) return false;

        return candidates.Any(l =>
        {
            // Compare against every translation in the LangStr dictionary so
            // e.g. "PPL" matches an entry stored as "PPL" in either 'en' or 'et'.
            var dict = l.LicenseType;
            if (dict == null) return false;
            foreach (var translation in dict.Values)
            {
                if (!string.IsNullOrEmpty(translation) &&
                    translation.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        });
    }
}
