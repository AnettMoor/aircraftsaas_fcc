using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IPersonService
{
    Task<IEnumerable<PersonDto>> GetAllForUserAsync(Guid userId);
    Task<PersonDto?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<PersonDto> CreateAsync(CreatePersonDto dto, Guid userId);
    Task<bool> UpdateAsync(Guid id, UpdatePersonDto dto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
}
