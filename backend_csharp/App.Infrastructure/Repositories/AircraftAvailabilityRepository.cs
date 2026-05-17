using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class AircraftAvailabilityRepository : BaseRepository<AircraftAvailability, AircraftAvailability, AppDbContext>, IAircraftAvailabilityRepository
{
    public AircraftAvailabilityRepository(AppDbContext dbContext, IBaseMapper<AircraftAvailability, AircraftAvailability> mapper)
        : base(dbContext, mapper)
    {
    }

    public AircraftAvailabilityRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<AircraftAvailability>())
    {
    }

    public async Task<IEnumerable<AircraftAvailability>> GetAllForAircraftAsync(Guid aircraftId)
    {
        return await RepositoryDbSet
            .Where(a => a.AircraftId == aircraftId)
            .OrderBy(a => a.StartDateTime)
            .ToListAsync();
    }

    public async Task<AircraftAvailability?> GetByIdForAircraftAsync(Guid id, Guid aircraftId)
    {
        return await RepositoryDbSet
            .FirstOrDefaultAsync(a => a.Id == id && a.AircraftId == aircraftId);
    }

    /// <summary>
    /// IDOR-safe tracking variant: fetches an availability record only if it belongs to the specified aircraft.
    /// </summary>
    public async Task<AircraftAvailability?> GetByIdForAircraftTrackingAsync(Guid id, Guid aircraftId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.AircraftId == aircraftId);
    }

    public async Task<AircraftAvailability?> GetByBookingIdTrackingAsync(Guid bookingId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(a => a.BookingId == bookingId);
    }

    public async Task<bool> HasBlockingAvailabilityAsync(Guid aircraftId, DateTime start, DateTime end)
    {
        return await RepositoryDbSet
            .Where(a => a.AircraftId == aircraftId &&
                        (a.AvailabilityType == "Blocked" || a.AvailabilityType == "Maintenance" || a.AvailabilityType == "Booked") &&
                        a.StartDateTime < end && a.EndDateTime > start)
            .AnyAsync();
    }
}
