using App.Domain;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IContactRepository : IBaseRepository<Contact>
{
    /// <summary>
    /// IDOR-safe: gets all contacts for a specific person.
    /// </summary>
    Task<IEnumerable<Contact>> GetAllForPersonAsync(Guid personId);
    
    /// <summary>
    /// IDOR-safe: gets a contact only if it belongs to the specified person.
    /// </summary>
    Task<Contact?> GetByIdForPersonAsync(Guid id, Guid personId);
    
    /// <summary>
    /// IDOR-safe tracking variant: gets a contact only if it belongs to the specified person.
    /// </summary>
    Task<Contact?> GetByIdForPersonTrackingAsync(Guid id, Guid personId);
    
    Task<Contact?> GetByIdTrackingAsync(Guid id);
}
