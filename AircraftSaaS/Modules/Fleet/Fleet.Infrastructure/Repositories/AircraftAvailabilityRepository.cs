using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Repositories;

internal sealed class AircraftAvailabilityRepository : BaseRepository<AircraftAvailability, AircraftAvailability, FleetDbContext>, IAircraftAvailabilityRepository
{
    public AircraftAvailabilityRepository(FleetDbContext dbContext)
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

    public async Task<AircraftAvailability?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
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
