using Shared.Kernel.DAL;
using Users.Domain.Entities;

namespace Users.Application.Contracts;

public interface IPersonRepository : IBaseRepository<Person>
{
    Task<Person?> GetByIdTrackingAsync(Guid id);
}
