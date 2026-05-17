using App.Domain.Contracts;
using App.Domain;
using App.Infrastructure.Mappers;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class ContactTypeRepository : BaseRepository<ContactType, ContactType, AppDbContext>, IContactTypeRepository
{
    public ContactTypeRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<ContactType>())
    {
    }

    public async Task<ContactType?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(ct => ct.Id == id);
    }
}
