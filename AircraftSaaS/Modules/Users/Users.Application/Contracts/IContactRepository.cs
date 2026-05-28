using Shared.Kernel.DAL;
using Users.Domain.Entities;

namespace Users.Application.Contracts;

public interface IContactRepository : IBaseRepository<Contact>
{
    Task<Contact?> GetByIdTrackingAsync(Guid id);
}
