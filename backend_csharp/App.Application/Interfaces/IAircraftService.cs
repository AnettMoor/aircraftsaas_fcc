using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IAircraftService
{
    Task<AircraftDto?> GetByIdAsync(Guid id, Guid? companyId = null);
    Task<IEnumerable<AircraftDto>> GetAllAsync(Guid companyId);
    Task<IEnumerable<AircraftDto>> GetAllDeletedAsync(Guid companyId);
    Task<IEnumerable<AircraftDto>> SearchAsync(AircraftSearchDto search);
    Task<AircraftDto> CreateAsync(CreateAircraftDto dto, Guid companyId, string createdBy);
    Task<AircraftDto> UpdateAsync(Guid id, UpdateAircraftDto dto, Guid companyId, string updatedBy);
    Task DeleteAsync(Guid id, Guid companyId, string deletedBy);
    Task RestoreAsync(Guid id, Guid companyId, string restoredBy);
    Task<IEnumerable<AircraftDto>> GetAvailableAsync(DateTime start, DateTime end, string? location = null);

    // Photo management
    Task<IEnumerable<AircraftPhotoDto>> GetPhotosAsync(Guid aircraftId, Guid? companyId = null);
    Task<AircraftPhotoDto> AddPhotoAsync(Guid aircraftId, Guid companyId, AddAircraftPhotoDto dto, string addedBy);
    Task SetPrimaryPhotoAsync(Guid photoId, Guid aircraftId, Guid companyId);
    Task DeletePhotoAsync(Guid photoId, Guid aircraftId, Guid companyId, string deletedBy);
}
