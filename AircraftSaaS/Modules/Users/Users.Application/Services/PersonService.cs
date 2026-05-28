using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Domain.Entities;

namespace Users.Application.Services;

internal sealed class PersonService : IPersonService
{
    private readonly IUsersUOW _uow;

    public PersonService(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<PersonDto>> GetAllAsync()
    {
        var persons = await _uow.PersonRepository.AllAsync();
        return persons.Select(MapToDto);
    }

    public async Task<PersonDto?> GetByIdAsync(Guid id)
    {
        var person = await _uow.PersonRepository.FindAsync(id);
        return person == null ? null : MapToDto(person);
    }

    public async Task<PersonDto> CreateAsync(CreatePersonDto dto)
    {
        var person = new Person
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            AppUserId = dto.AppUserId,
            CompanyId = dto.CompanyId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        };

        _uow.PersonRepository.Add(person);
        await _uow.SaveChangesAsync();

        return MapToDto(person);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdatePersonDto dto)
    {
        var person = await _uow.PersonRepository.GetByIdTrackingAsync(id);
        if (person == null) return false;

        person.FirstName = dto.FirstName;
        person.LastName = dto.LastName;
        person.UpdatedBy = "system";
        person.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var person = await _uow.PersonRepository.FindAsync(id);
        if (person == null) return false;

        _uow.PersonRepository.Remove(person);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static PersonDto MapToDto(Person person)
    {
        return new PersonDto
        {
            Id = person.Id,
            FirstName = person.FirstName,
            LastName = person.LastName,
            AppUserId = person.AppUserId,
            CompanyId = person.CompanyId
        };
    }
}
