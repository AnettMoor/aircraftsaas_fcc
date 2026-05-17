using App.Domain.Entities;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IAircraftAvailabilityRepository : IBaseRepository<AircraftAvailability>
{
    Task<IEnumerable<AircraftAvailability>> GetAllForAircraftAsync(Guid aircraftId);
    Task<AircraftAvailability?> GetByIdForAircraftAsync(Guid id, Guid aircraftId);
    
    /// <summary>
    /// IDOR-safe tracking variant: fetches an availability record only if it belongs to the specified aircraft.
    /// </summary>
    Task<AircraftAvailability?> GetByIdForAircraftTrackingAsync(Guid id, Guid aircraftId);
    
    Task<AircraftAvailability?> GetByBookingIdTrackingAsync(Guid bookingId);
    Task<bool> HasBlockingAvailabilityAsync(Guid aircraftId, DateTime start, DateTime end);
}
