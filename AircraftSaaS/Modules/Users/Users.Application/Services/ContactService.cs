using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Domain.Entities;

namespace Users.Application.Services;

internal sealed class ContactService : IContactService
{
    private readonly IUsersUOW _uow;

    public ContactService(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<ContactDto>> GetAllAsync()
    {
        var contacts = await _uow.ContactRepository.AllAsync();
        return contacts.Select(MapToDto);
    }

    public async Task<ContactDto?> GetByIdAsync(Guid id)
    {
        var contact = await _uow.ContactRepository.FindAsync(id);
        return contact == null ? null : MapToDto(contact);
    }

    public async Task<ContactDto> CreateAsync(CreateContactDto dto)
    {
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

    public async Task<bool> UpdateAsync(Guid id, UpdateContactDto dto)
    {
        var contact = await _uow.ContactRepository.GetByIdTrackingAsync(id);
        if (contact == null) return false;

        contact.ContactValue = dto.ContactValue;
        contact.ContactTypeId = dto.ContactTypeId;
        contact.PersonId = dto.PersonId;
        contact.UpdatedBy = "system";
        contact.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var contact = await _uow.ContactRepository.FindAsync(id);
        if (contact == null) return false;

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
