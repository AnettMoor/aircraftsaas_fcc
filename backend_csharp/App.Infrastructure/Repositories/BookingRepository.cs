using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using App.Domain.Enums;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class BookingRepository : BaseRepository<Booking, Booking, AppDbContext>, IBookingRepository
{
    public BookingRepository(AppDbContext dbContext, IBaseMapper<Booking, Booking> mapper)
        : base(dbContext, mapper)
    {
    }

    public BookingRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<Booking>())
    {
    }
    
    //for pilots and companyowners
    public async Task<Booking?> GetByIdWithIncludesAsync(Guid id, Guid? companyId = null, Guid? userId = null)
    {
        //idor applied for companies
        var query = GetFilteredQuery(companyId: companyId);

        //idor applied if pilot
        if (userId.HasValue && userId.Value != default)
        {
            query = query.Where(b => b.PilotId == userId.Value);
        }

        query = query
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .Include(b => b.Payments);

        return await query.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Booking>> GetAllForPilotAsync(Guid userId)
    {
        return await GetFilteredQuery()
            .Where(b => b.PilotId == userId)
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .Include(b => b.Payments)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetAllForCompanyAsync(Guid companyId)
    {
        return await GetFilteredQuery(companyId: companyId)
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .Include(b => b.Payments)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> HasOverlappingBookingsAsync(Guid aircraftId, DateTime start, DateTime end, Guid? excludeBookingId = null)
    {
        var query = RepositoryDbSet
            .Where(b => b.AircraftId == aircraftId &&
                        b.Status != EBookingStatus.Cancelled &&
                        b.Status != EBookingStatus.Rejected &&
                        ((start >= b.StartDateTime && start < b.EndDateTime) ||
                         (end > b.StartDateTime && end <= b.EndDateTime) ||
                         (start <= b.StartDateTime && end >= b.EndDateTime)));

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync();
    }

    public async Task<Booking?> GetByIdTrackingWithIncludesAsync(Guid id, Guid? companyId = null)
    {
        var query = GetFilteredQuery(companyId: companyId)
            .AsTracking()
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .Include(b => b.Payments);

        return await query.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Booking?> GetByIdTrackingWithPaymentsAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Booking?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Booking?> GetByIdForPilotAsync(Guid bookingId, Guid pilotId)
    {
        return await GetFilteredQuery()
            .Where(b => b.PilotId == pilotId)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    // Cross-entity helper methods
    public async Task<bool> UserExistsAsync(Guid userId)
    {
        return await RepositoryDbContext.Users.AnyAsync(u => u.Id == userId);
    }

    public async Task<bool> HasValidLicenseAsync(Guid userId, string licenseType, DateTime bookingDate)
    {
        // LicenseType is a LangStr (stored as JSON, e.g. {"en":"PPL"}).
        // EF Core cannot translate a LangStr == string comparison to SQL,
        // so we filter by user & expiry in SQL, then match the type in memory.
        var userLicenses = await RepositoryDbContext.Licenses
            .Where(l => l.AppUserId == userId && l.ExpiryDate > bookingDate)
            .ToListAsync();

        return userLicenses.Any(l =>
            string.Equals(l.LicenseType.ToString(), licenseType, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> HasAnyLicenseAsync(Guid userId)
    {
        return await RepositoryDbContext.Licenses.AnyAsync(l => l.AppUserId == userId);
    }

    public async Task<bool> HasInsuranceCoverageAsync(Guid aircraftId, DateTime start, DateTime end)
    {
        return await RepositoryDbContext.InsurancePolicies
            .AnyAsync(i => i.AircraftId == aircraftId &&
                          i.StartDate <= start &&
                          i.EndDate >= end);
    }

    public async Task<bool> HasAnyInsuranceAsync(Guid aircraftId)
    {
        return await RepositoryDbContext.InsurancePolicies.AnyAsync(i => i.AircraftId == aircraftId);
    }

    public async Task<bool> IsCompanyOwnerAsync(Guid userId, Guid companyId)
    {
        return await RepositoryDbContext.AppUserCompanies
            .AnyAsync(uc => uc.AppUserId == userId &&
                           uc.CompanyId == companyId &&
                           uc.AppUserRoleInCompany == Domain.EAppUserRoleInCompany.CompanyOwner);
    }

    public void AddPayment(Payment payment)
    {
        RepositoryDbContext.Payments.Add(payment);
    }

    // System-admin methods
    
    public async Task<int> CountAllAsync()
    {
        return await RepositoryDbSet.CountAsync();
    }

    public async Task<int> CountByPilotAsync(Guid pilotId)
    {
        return await RepositoryDbSet.CountAsync(b => b.PilotId == pilotId);
    }

    public async Task<int> CountByCompanyAsync(Guid companyId)
    {
        return await RepositoryDbSet.CountAsync(b => b.CompanyId == companyId);
    }

    public async Task<IEnumerable<Booking>> GetActiveForPilotTrackingAsync(Guid pilotId)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Where(b => b.PilotId == pilotId &&
                        b.Status != EBookingStatus.Completed &&
                        b.Status != EBookingStatus.Cancelled &&
                        b.Status != EBookingStatus.Rejected)
            .ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetAllSystemWideWithIncludesAsync()
    {
        return await RepositoryDbSet
            .Include(b => b.Aircraft)
            .Include(b => b.Pilot)
            .Include(b => b.Company)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }
}
