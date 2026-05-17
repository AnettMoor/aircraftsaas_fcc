using App.Domain;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IPersonRepository : IBaseRepository<Person>
{
    /// <summary>
    /// IDOR-safe: fetches a person only if it belongs to the given user.
    /// </summary>
    Task<Person?> GetByIdForUserAsync(Guid id, Guid userId);
    
    /// <summary>
    /// IDOR-safe tracking variant: fetches a person only if it belongs to the given user.
    /// </summary>
    Task<Person?> GetByIdForUserTrackingAsync(Guid id, Guid userId);
    
    Task<Person?> GetByIdTrackingAsync(Guid id);
}
