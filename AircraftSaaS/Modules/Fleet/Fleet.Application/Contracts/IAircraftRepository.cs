using Fleet.Domain.Entities;
using Shared.Kernel.DAL;

namespace Fleet.Application.Contracts;

public interface IAircraftRepository : IBaseRepository<Aircraft>
{
    // Custom query methods beyond base CRUD
    Task<Aircraft?> GetByIdWithIncludesAsync(Guid id, Guid? companyId = null);
    Task<IEnumerable<Aircraft>> GetAllForCompanyAsync(Guid companyId);
    Task<IEnumerable<Aircraft>> GetAllWithIncludesForCompanyAsync(Guid companyId);
    Task<IEnumerable<Aircraft>> GetAllDeletedForCompanyAsync(Guid companyId);
    Task<IEnumerable<Aircraft>> GetAvailableAsync(DateTime start, DateTime end, string? location = null);
    Task<IEnumerable<Aircraft>> SearchAsync(
        string? make = null,
        string? model = null,
        string? category = null,
        string? location = null,
        decimal? maxHourlyRate = null,
        int? year = null,
        bool? available = null,
        int page = 1,
        int pageSize = 20);
    Task<bool> ExistsForCompanyAsync(Guid id, Guid companyId);
    Task<int> GetCountForCompanyAsync(Guid companyId);
    Task<IEnumerable<Aircraft>> GetByBaseAirportAsync(Guid airportId);
    Task<Aircraft?> GetByIdForCompanyTrackingAsync(Guid id, Guid companyId);
    Task<Aircraft?> GetByIdIgnoreFiltersTrackingAsync(Guid id, Guid companyId);
    Task<Aircraft?> GetDeletedByIdTrackingAsync(Guid id, Guid companyId);
    
    // Photo methods
    Task<IEnumerable<AircraftPhoto>> GetPhotosAsync(Guid aircraftId);
    Task<AircraftPhoto?> GetPhotoByIdTrackingAsync(Guid photoId, Guid aircraftId);
    Task<IEnumerable<AircraftPhoto>> GetPrimaryPhotosTrackingAsync(Guid aircraftId);
    Task<int?> GetMaxPhotoDisplayOrderAsync(Guid aircraftId);
    void AddPhoto(AircraftPhoto photo);
    
    // Batch methods (for cross-module API handlers)
    Task<List<Aircraft>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    
    // Insurance methods
    Task<IEnumerable<InsurancePolicy>> GetInsurancePoliciesAsync(Guid aircraftId);
    void AddInsurancePolicy(InsurancePolicy policy);
}
