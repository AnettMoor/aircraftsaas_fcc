using Shared.Kernel.DAL;
using Users.Domain.Entities;

namespace Users.Application.Contracts;

public interface IAppUserCompanyRepository : IBaseRepository<AppUserCompany>
{
    Task<AppUserCompany?> GetByIdTrackingAsync(Guid id);
}
