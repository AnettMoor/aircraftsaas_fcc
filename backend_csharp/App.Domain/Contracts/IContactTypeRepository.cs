using App.Domain;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IContactTypeRepository : IBaseRepository<ContactType>
{
    Task<ContactType?> GetByIdTrackingAsync(Guid id);
}
