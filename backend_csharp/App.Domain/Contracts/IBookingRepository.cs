using App.Domain.Entities;
using App.Domain.Enums;
using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IBookingRepository : IBaseRepository<Booking>
{
    Task<Booking?> GetByIdWithIncludesAsync(Guid id, Guid? companyId = null, Guid? userId = null);
    Task<IEnumerable<Booking>> GetAllForPilotAsync(Guid userId);
    Task<IEnumerable<Booking>> GetAllForCompanyAsync(Guid companyId);
    Task<bool> HasOverlappingBookingsAsync(Guid aircraftId, DateTime start, DateTime end, Guid? excludeBookingId = null);
    Task<Booking?> GetByIdTrackingWithIncludesAsync(Guid id, Guid? companyId = null);
    Task<Booking?> GetByIdTrackingWithPaymentsAsync(Guid id);
    Task<Booking?> GetByIdTrackingAsync(Guid id);
    Task<Booking?> GetByIdForPilotAsync(Guid bookingId, Guid pilotId);
    
    // Cross-entity helper methods for booking validation
    Task<bool> UserExistsAsync(Guid userId);
    Task<bool> HasValidLicenseAsync(Guid userId, string licenseType, DateTime bookingDate);
    Task<bool> HasAnyLicenseAsync(Guid userId);
    Task<bool> HasInsuranceCoverageAsync(Guid aircraftId, DateTime start, DateTime end);
    Task<bool> HasAnyInsuranceAsync(Guid aircraftId);
    Task<bool> IsCompanyOwnerAsync(Guid userId, Guid companyId);
    void AddPayment(Payment payment);
    
    // System-admin methods
    Task<int> CountAllAsync();
    Task<int> CountByPilotAsync(Guid pilotId);
    Task<int> CountByCompanyAsync(Guid companyId);
    Task<IEnumerable<Booking>> GetActiveForPilotTrackingAsync(Guid pilotId);
    Task<IEnumerable<Booking>> GetAllSystemWideWithIncludesAsync();
}
