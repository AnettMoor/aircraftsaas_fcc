using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IContactService
{
    Task<IEnumerable<ContactDto>> GetAllForUserAsync(Guid userId);
    Task<ContactDto?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<ContactDto> CreateAsync(CreateContactDto dto, Guid userId);
    Task<bool> UpdateAsync(Guid id, UpdateContactDto dto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
}
