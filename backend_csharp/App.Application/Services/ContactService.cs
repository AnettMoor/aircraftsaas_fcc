using App.Domain.Contracts;
using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain;

namespace App.Application.Services;

public class ContactService : IContactService
{
    private readonly IAppUOW _uow;

    public ContactService(IAppUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<ContactDto>> GetAllForUserAsync(Guid userId)
    {
        // IDOR: only return contacts whose Person belongs to the current user.
        // First get all person IDs owned by this user, then fetch their contacts.
        var persons = await _uow.PersonRepository.AllAsync(appUserId: userId);
        var personIds = persons.Select(p => p.Id).ToHashSet();

        var allContacts = new List<ContactDto>();
        foreach (var personId in personIds)
        {
            var contacts = await _uow.ContactRepository.GetAllForPersonAsync(personId);
            allContacts.AddRange(contacts.Select(MapToDto));
        }

        return allContacts;
    }

    public async Task<ContactDto?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        // IDOR: verify the contact's person belongs to the current user
        var contact = await _uow.ContactRepository.FindAsync(id);
        if (contact == null) return null;

        var person = await _uow.PersonRepository.GetByIdForUserAsync(contact.PersonId, userId);
        if (person == null) return null; // Person doesn't belong to user → IDOR block

        return MapToDto(contact);
    }

    public async Task<ContactDto> CreateAsync(CreateContactDto dto, Guid userId)
    {
        // IDOR: verify the target person belongs to the current user
        var person = await _uow.PersonRepository.GetByIdForUserAsync(dto.PersonId, userId);
        if (person == null)
            throw new InvalidOperationException("Person not found or does not belong to the current user.");

        var contact = new Contact
        {
            ContactValue = dto.ContactValue,
            ContactTypeId = dto.ContactTypeId,
            PersonId = dto.PersonId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        };

        _uow.ContactRepository.Add(contact);
        await _uow.SaveChangesAsync();

        return MapToDto(contact);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateContactDto dto, Guid userId)
    {
        // IDOR: verify the existing contact's person belongs to the current user
        var contact = await _uow.ContactRepository.GetByIdTrackingAsync(id);
        if (contact == null) return false;

        var person = await _uow.PersonRepository.GetByIdForUserAsync(contact.PersonId, userId);
        if (person == null) return false; // Person doesn't belong to user → IDOR block

        // If updating to a different person, verify that person also belongs to the user
        if (dto.PersonId != contact.PersonId)
        {
            var newPerson = await _uow.PersonRepository.GetByIdForUserAsync(dto.PersonId, userId);
            if (newPerson == null) return false;
        }

        contact.ContactValue = dto.ContactValue;
        contact.ContactTypeId = dto.ContactTypeId;
        contact.PersonId = dto.PersonId;
        contact.UpdatedBy = "system";
        contact.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        // IDOR: verify the contact's person belongs to the current user
        var contact = await _uow.ContactRepository.FindAsync(id);
        if (contact == null) return false;

        var person = await _uow.PersonRepository.GetByIdForUserAsync(contact.PersonId, userId);
        if (person == null) return false; // Person doesn't belong to user → IDOR block

        _uow.ContactRepository.Remove(contact);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static ContactDto MapToDto(Contact contact)
    {
        return new ContactDto
        {
            Id = contact.Id,
            ContactValue = contact.ContactValue,
            ContactTypeId = contact.ContactTypeId,
            PersonId = contact.PersonId
        };
    }
}
