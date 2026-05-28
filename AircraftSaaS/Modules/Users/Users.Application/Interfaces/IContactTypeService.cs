using Users.Application.DTOs;

namespace Users.Application.Interfaces;

public interface IContactTypeService
{
    Task<IEnumerable<ContactTypeDto>> GetAllAsync();
    Task<ContactTypeDto?> GetByIdAsync(Guid id);
    Task<ContactTypeDto> CreateAsync(CreateContactTypeDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateContactTypeDto dto);
    Task<bool> DeleteAsync(Guid id);
}
