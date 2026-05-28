using Microsoft.EntityFrameworkCore;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;

namespace Users.Infrastructure.Repositories;

internal sealed class AppUserCompanyRepository : BaseRepository<AppUserCompany, AppUserCompany, UsersDbContext>, IAppUserCompanyRepository
{
    public AppUserCompanyRepository(UsersDbContext dbContext)
        : base(dbContext, new BaseMapper<AppUserCompany>())
    {
    }

    public async Task<AppUserCompany?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet.AsTracking().FirstOrDefaultAsync(auc => auc.Id == id);
    }
}
