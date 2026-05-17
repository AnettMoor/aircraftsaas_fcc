using App.Domain.Contracts;
using App.Domain;
using App.Infrastructure.Mappers;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class ContactRepository : BaseRepository<Contact, Contact, AppDbContext>, IContactRepository
{
    public ContactRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<Contact>())
    {
    }

    /// <summary>
    /// IDOR-safe: gets all contacts for a specific person.
    /// </summary>
    public async Task<IEnumerable<Contact>> GetAllForPersonAsync(Guid personId)
    {
        return await RepositoryDbSet
            .Where(c => c.PersonId == personId)
            .ToListAsync();
    }

    /// <summary>
    /// IDOR-safe: gets a contact only if it belongs to the specified person.
    /// </summary>
    public async Task<Contact?> GetByIdForPersonAsync(Guid id, Guid personId)
    {
        return await RepositoryDbSet
            .FirstOrDefaultAsync(c => c.Id == id && c.PersonId == personId);
    }

    /// <summary>
    /// IDOR-safe tracking variant: gets a contact only if it belongs to the specified person.
    /// </summary>
    public async Task<Contact?> GetByIdForPersonTrackingAsync(Guid id, Guid personId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.PersonId == personId);
    }

    public async Task<Contact?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(c => c.Id == id);
    }
}
