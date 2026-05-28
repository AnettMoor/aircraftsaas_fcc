using Users.Application.DTOs;

namespace Users.Application.Interfaces;

public interface IContactService
{
    Task<IEnumerable<ContactDto>> GetAllAsync();
    Task<ContactDto?> GetByIdAsync(Guid id);
    Task<ContactDto> CreateAsync(CreateContactDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateContactDto dto);
    Task<bool> DeleteAsync(Guid id);
}
