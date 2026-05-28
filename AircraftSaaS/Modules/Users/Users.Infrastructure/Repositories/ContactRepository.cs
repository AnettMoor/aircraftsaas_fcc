using Microsoft.EntityFrameworkCore;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;

namespace Users.Infrastructure.Repositories;

internal sealed class ContactRepository : BaseRepository<Contact, Contact, UsersDbContext>, IContactRepository
{
    public ContactRepository(UsersDbContext dbContext)
        : base(dbContext, new BaseMapper<Contact>())
    {
    }

    public async Task<Contact?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(c => c.Id == id);
    }
}
