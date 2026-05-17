using App.Domain.Contracts;
using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain;

namespace App.Application.Services;

public class PersonService : IPersonService
{
    private readonly IAppUOW _uow;

    public PersonService(IAppUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<PersonDto>> GetAllForUserAsync(Guid userId)
    {
        // IDOR: only return persons belonging to the current user
        var persons = await _uow.PersonRepository.AllAsync(appUserId: userId);
        return persons.Select(MapToDto);
    }

    public async Task<PersonDto?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        // IDOR: only return the person if it belongs to the current user
        var person = await _uow.PersonRepository.GetByIdForUserAsync(id, userId);
        return person == null ? null : MapToDto(person);
    }

    public async Task<PersonDto> CreateAsync(CreatePersonDto dto, Guid userId)
    {
        // IDOR: force the AppUserId to the authenticated user so users
        // cannot create Person records belonging to someone else
        var person = new Person
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            AppUserId = userId,
            CompanyId = dto.CompanyId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        };

        _uow.PersonRepository.Add(person);
        await _uow.SaveChangesAsync();

        return MapToDto(person);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdatePersonDto dto, Guid userId)
    {
        // IDOR: only allow update if the person belongs to the current user
        var person = await _uow.PersonRepository.GetByIdForUserTrackingAsync(id, userId);
        if (person == null) return false;

        person.FirstName = dto.FirstName;
        person.LastName = dto.LastName;
        person.UpdatedBy = "system";
        person.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        // IDOR: only allow delete if the person belongs to the current user
        var person = await _uow.PersonRepository.FindAsync(id, appUserId: userId);
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
