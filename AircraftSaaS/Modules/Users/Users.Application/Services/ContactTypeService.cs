using Shared.Kernel.Domain;
using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Domain.Entities;

namespace Users.Application.Services;

internal sealed class ContactTypeService : IContactTypeService
{
    private readonly IUsersUOW _uow;

    public ContactTypeService(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<ContactTypeDto>> GetAllAsync()
    {
        var contactTypes = await _uow.ContactTypeRepository.AllAsync();
        return contactTypes.Select(MapToDto);
    }

    public async Task<ContactTypeDto?> GetByIdAsync(Guid id)
    {
        var contactType = await _uow.ContactTypeRepository.FindAsync(id);
        return contactType == null ? null : MapToDto(contactType);
    }

    public async Task<ContactTypeDto> CreateAsync(CreateContactTypeDto dto)
    {
        var contactType = new ContactType
        {
            ContactTypeName = new LangStr(dto.ContactTypeName),
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        };

        _uow.ContactTypeRepository.Add(contactType);
        await _uow.SaveChangesAsync();

        return MapToDto(contactType);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateContactTypeDto dto)
    {
        var contactType = await _uow.ContactTypeRepository.GetByIdTrackingAsync(id);
        if (contactType == null) return false;

        contactType.ContactTypeName.SetTranslation(dto.ContactTypeName);
        contactType.UpdatedBy = "system";
        contactType.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var contactType = await _uow.ContactTypeRepository.FindAsync(id);
        if (contactType == null) return false;

        _uow.ContactTypeRepository.Remove(contactType);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static ContactTypeDto MapToDto(ContactType contactType)
    {
        return new ContactTypeDto
        {
            Id = contactType.Id,
            ContactTypeName = contactType.ContactTypeName.ToString()
        };
    }
}
