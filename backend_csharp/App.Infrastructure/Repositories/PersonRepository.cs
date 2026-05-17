using App.Domain.Contracts;
using App.Domain;
using App.Infrastructure.Mappers;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class PersonRepository : BaseRepository<Person, Person, AppDbContext>, IPersonRepository
{
    public PersonRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<Person>())
    {
    }

    /// <summary>
    /// IDOR-safe: fetches a person only if it belongs to the given user.
    /// </summary>
    public async Task<Person?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        return await GetFilteredQuery(appUserId: userId)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>
    /// IDOR-safe tracking variant: fetches a person only if it belongs to the given user.
    /// </summary>
    public async Task<Person?> GetByIdForUserTrackingAsync(Guid id, Guid userId)
    {
        return await GetFilteredQuery(appUserId: userId)
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Person?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(p => p.Id == id);
    }
}
