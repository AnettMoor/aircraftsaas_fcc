using Microsoft.EntityFrameworkCore;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;

namespace Users.Infrastructure.Repositories;

internal sealed class PersonRepository : BaseRepository<Person, Person, UsersDbContext>, IPersonRepository
{
    public PersonRepository(UsersDbContext dbContext)
        : base(dbContext, new BaseMapper<Person>())
    {
    }

    public async Task<Person?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(p => p.Id == id);
    }
}
