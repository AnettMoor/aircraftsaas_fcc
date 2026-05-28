using Shared.Kernel.DAL;
using Users.Domain.Entities;

namespace Users.Application.Contracts;

public interface IContactTypeRepository : IBaseRepository<ContactType>
{
    Task<ContactType?> GetByIdTrackingAsync(Guid id);
}
