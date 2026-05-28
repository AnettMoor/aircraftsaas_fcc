using Users.Application.DTOs;

namespace Users.Application.Interfaces;

public interface IPersonService
{
    Task<IEnumerable<PersonDto>> GetAllAsync();
    Task<PersonDto?> GetByIdAsync(Guid id);
    Task<PersonDto> CreateAsync(CreatePersonDto dto);
    Task<bool> UpdateAsync(Guid id, UpdatePersonDto dto);
    Task<bool> DeleteAsync(Guid id);
}
