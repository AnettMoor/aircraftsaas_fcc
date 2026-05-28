using Microsoft.EntityFrameworkCore;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;

namespace Users.Infrastructure.Repositories;

internal sealed class ContactTypeRepository : BaseRepository<ContactType, ContactType, UsersDbContext>, IContactTypeRepository
{
    public ContactTypeRepository(UsersDbContext dbContext)
        : base(dbContext, new BaseMapper<ContactType>())
    {
    }

    public async Task<ContactType?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(ct => ct.Id == id);
    }
}
