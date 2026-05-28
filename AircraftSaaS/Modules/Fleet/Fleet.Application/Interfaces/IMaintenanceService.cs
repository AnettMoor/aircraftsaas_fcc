using Fleet.Application.DTOs;

namespace Fleet.Application.Interfaces;

public interface IMaintenanceService
{
    Task<IEnumerable<MaintenanceRecordDto>> GetAllForCompanyAsync(Guid companyId, Guid? aircraftId = null);
    Task<MaintenanceRecordDto?> GetByIdAsync(Guid id, Guid companyId);
    Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordDto dto, Guid companyId, string createdBy);
    Task<MaintenanceRecordDto> UpdateAsync(Guid id, UpdateMaintenanceRecordDto dto, Guid companyId, string updatedBy);
    Task DeleteAsync(Guid id, Guid companyId, string deletedBy);
    Task RestoreAsync(Guid id, Guid companyId);
}
