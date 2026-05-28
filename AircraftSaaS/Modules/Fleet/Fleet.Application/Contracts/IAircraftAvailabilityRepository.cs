using Fleet.Domain.Entities;
using Shared.Kernel.DAL;

namespace Fleet.Application.Contracts;

public interface IAircraftAvailabilityRepository : IBaseRepository<AircraftAvailability>
{
    Task<IEnumerable<AircraftAvailability>> GetAllForAircraftAsync(Guid aircraftId);
    Task<AircraftAvailability?> GetByIdForAircraftAsync(Guid id, Guid aircraftId);
    Task<AircraftAvailability?> GetByIdTrackingAsync(Guid id);
    Task<AircraftAvailability?> GetByBookingIdTrackingAsync(Guid bookingId);
    Task<bool> HasBlockingAvailabilityAsync(Guid aircraftId, DateTime start, DateTime end);
}
