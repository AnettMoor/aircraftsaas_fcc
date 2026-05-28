using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Repositories;

internal sealed class AircraftRepository : BaseRepository<Aircraft, Aircraft, FleetDbContext>, IAircraftRepository
{
    public AircraftRepository(FleetDbContext dbContext)
        : base(dbContext, new BaseMapper<Aircraft>())
    {
    }

    public async Task<Aircraft?> GetByIdWithIncludesAsync(Guid id, Guid? companyId = null)
    {
        var query = GetFilteredQuery(companyId: companyId)
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Include(a => a.Availabilities)
            .Include(a => a.InsurancePolicies)
            .Include(a => a.MaintenanceRecords);

        return await query.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Aircraft>> GetAllForCompanyAsync(Guid companyId)
    {
        var results = await GetFilteredQuery(companyId: companyId)
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Include(a => a.InsurancePolicies)
            .Include(a => a.MaintenanceRecords)
            .ToListAsync();

        return results.OrderBy(a => a.Make.ToString()).ThenBy(a => a.Model.ToString());
    }

    public async Task<IEnumerable<Aircraft>> GetAllWithIncludesForCompanyAsync(Guid companyId)
    {
        var results = await GetFilteredQuery(companyId: companyId)
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Include(a => a.Availabilities)
            .Include(a => a.InsurancePolicies)
            .ToListAsync();

        return results.OrderBy(a => a.Make.ToString()).ThenBy(a => a.Model.ToString());
    }

    public async Task<IEnumerable<Aircraft>> GetAllDeletedForCompanyAsync(Guid companyId)
    {
        var results = await RepositoryDbSet
            .IgnoreQueryFilters()
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Include(a => a.InsurancePolicies)
            .Where(a => a.CompanyId == companyId && a.DeletedAt != null)
            .ToListAsync();

        return results.OrderBy(a => a.Make.ToString()).ThenBy(a => a.Model.ToString());
    }

    public async Task<IEnumerable<Aircraft>> GetAvailableAsync(DateTime start, DateTime end, string? location = null)
    {
        // Public catalog — no IDOR filtering (intentionally unscoped)
        var query = RepositoryDbSet
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Include(a => a.Availabilities)
            .Include(a => a.InsurancePolicies)
            .Include(a => a.MaintenanceRecords)
            .Where(a => a.IsAvailable);

        var aircraftList = await query.ToListAsync();

        IEnumerable<Aircraft> filtered = aircraftList;
        if (!string.IsNullOrEmpty(location))
        {
            filtered = filtered.Where(a =>
                (a.BaseAirport?.City.ToString()?.Contains(location, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.BaseAirport?.Name.ToString()?.Contains(location, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Instead of checking Bookings (cross-module), check AircraftAvailability blocks
        return filtered.Where(a => !a.Availabilities!.Any(av =>
            (av.AvailabilityType == "Blocked" || av.AvailabilityType == "Maintenance" || av.AvailabilityType == "Booked") &&
            av.StartDateTime < end && av.EndDateTime > start));
    }

    public async Task<IEnumerable<Aircraft>> SearchAsync(
        string? make = null,
        string? model = null,
        string? category = null,
        string? location = null,
        decimal? maxHourlyRate = null,
        int? year = null,
        bool? available = null,
        int page = 1,
        int pageSize = 20)
    {
        // Public search — no IDOR filtering (intentionally unscoped)
        var query = RepositoryDbSet
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Include(a => a.Availabilities)
            .Include(a => a.InsurancePolicies)
            .Include(a => a.MaintenanceRecords)
            .AsQueryable();

        if (maxHourlyRate.HasValue)
            query = query.Where(a => a.HourlyRate <= maxHourlyRate.Value);

        if (year.HasValue)
            query = query.Where(a => a.Year == year.Value);

        if (available.HasValue)
            query = query.Where(a => a.IsAvailable == available.Value);

        var allResults = await query.ToListAsync();
        IEnumerable<Aircraft> filtered = allResults;

        if (!string.IsNullOrEmpty(make))
            filtered = filtered.Where(a => a.Make.ToString()?.Contains(make, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!string.IsNullOrEmpty(model))
            filtered = filtered.Where(a => a.Model.ToString()?.Contains(model, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!string.IsNullOrEmpty(category))
            filtered = filtered.Where(a => a.Category.ToString()?.Contains(category, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!string.IsNullOrEmpty(location))
            filtered = filtered.Where(a =>
                (a.BaseAirport?.City.ToString()?.Contains(location, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.BaseAirport?.Name.ToString()?.Contains(location, StringComparison.OrdinalIgnoreCase) ?? false));

        var skip = (page - 1) * pageSize;
        return filtered.OrderBy(a => a.HourlyRate).Skip(skip).Take(pageSize).ToList();
    }

    public async Task<bool> ExistsForCompanyAsync(Guid id, Guid companyId)
    {
        return await GetFilteredQuery(companyId: companyId).AnyAsync(a => a.Id == id);
    }

    public async Task<int> GetCountForCompanyAsync(Guid companyId)
    {
        return await GetFilteredQuery(companyId: companyId).CountAsync();
    }

    public async Task<IEnumerable<Aircraft>> GetByBaseAirportAsync(Guid airportId)
    {
        return await RepositoryDbSet
            .Include(a => a.BaseAirport)
            .Include(a => a.Photos)
            .Where(a => a.BaseAirportId == airportId)
            .ToListAsync();
    }

    public async Task<Aircraft?> GetByIdForCompanyTrackingAsync(Guid id, Guid companyId)
    {
        return await GetFilteredQuery(companyId: companyId)
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Aircraft?> GetByIdIgnoreFiltersTrackingAsync(Guid id, Guid companyId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
    }

    public async Task<Aircraft?> GetDeletedByIdTrackingAsync(Guid id, Guid companyId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && a.DeletedAt != null);
    }

    // ── Photo methods ──

    public async Task<IEnumerable<AircraftPhoto>> GetPhotosAsync(Guid aircraftId)
    {
        return await RepositoryDbContext.Set<AircraftPhoto>()
            .Where(p => p.AircraftId == aircraftId)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.UploadedAt)
            .ToListAsync();
    }

    public async Task<AircraftPhoto?> GetPhotoByIdTrackingAsync(Guid photoId, Guid aircraftId)
    {
        return await RepositoryDbContext.Set<AircraftPhoto>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId && p.AircraftId == aircraftId);
    }

    public async Task<IEnumerable<AircraftPhoto>> GetPrimaryPhotosTrackingAsync(Guid aircraftId)
    {
        return await RepositoryDbContext.Set<AircraftPhoto>()
            .AsTracking()
            .Where(p => p.AircraftId == aircraftId && p.IsPrimary)
            .ToListAsync();
    }

    public async Task<int?> GetMaxPhotoDisplayOrderAsync(Guid aircraftId)
    {
        return await RepositoryDbContext.Set<AircraftPhoto>()
            .Where(p => p.AircraftId == aircraftId)
            .Select(p => (int?)p.DisplayOrder)
            .MaxAsync();
    }

    public void AddPhoto(AircraftPhoto photo)
    {
        RepositoryDbContext.Set<AircraftPhoto>().Add(photo);
    }

    // ── Insurance methods ──

    public async Task<IEnumerable<InsurancePolicy>> GetInsurancePoliciesAsync(Guid aircraftId)
    {
        return await RepositoryDbContext.Set<InsurancePolicy>()
            .Where(p => p.AircraftId == aircraftId)
            .OrderByDescending(p => p.EndDate)
            .ToListAsync();
    }

    public void AddInsurancePolicy(InsurancePolicy policy)
    {
        RepositoryDbContext.Set<InsurancePolicy>().Add(policy);
    }

    // ── Cross-module API support ──

    public async Task<List<Aircraft>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await RepositoryDbSet
            .Where(a => idList.Contains(a.Id))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> CountAllAsync(CancellationToken ct = default)
    {
        return await RepositoryDbSet.CountAsync(ct);
    }
}
