using Booking.Application.Contracts;
using Booking.Domain.Entities;
using Booking.Domain.Enums;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Repositories;

/// <summary>
/// Booking-module repository. Cross-module methods (HasValidLicenseAsync, HasInsuranceCoverageAsync,
/// UserExistsAsync, IsCompanyOwnerAsync) have been removed — those checks now happen in the
/// service layer via MediatR calls to the Users and Fleet modules.
/// </summary>
internal sealed class BookingRepository : BaseRepository<Domain.Entities.Booking, Domain.Entities.Booking, BookingDbContext>, IBookingRepository
{
    public BookingRepository(BookingDbContext dbContext, IBaseMapper<Domain.Entities.Booking, Domain.Entities.Booking> mapper)
        : base(dbContext, mapper)
    {
    }

    public BookingRepository(BookingDbContext dbContext)
        : base(dbContext, new BaseMapper<Domain.Entities.Booking>())
    {
    }

    public async Task<Domain.Entities.Booking?> GetByIdWithIncludesAsync(Guid id, Guid? companyId = null, Guid? userId = null)
    {
        var query = GetFilteredQuery(companyId: companyId);

        if (userId.HasValue && userId.Value != default)
        {
            query = query.Where(b => b.PilotId == userId.Value);
        }

        query = query
            .Include(b => b.Payments)
            .Include(b => b.Reviews);

        return await query.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Domain.Entities.Booking>> GetAllForPilotAsync(Guid userId)
    {
        return await GetFilteredQuery()
            .Where(b => b.PilotId == userId)
            .Include(b => b.Payments)
            .Include(b => b.Reviews)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Domain.Entities.Booking>> GetAllForCompanyAsync(Guid companyId)
    {
        return await GetFilteredQuery(companyId: companyId)
            .Include(b => b.Payments)
            .Include(b => b.Reviews)
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

    public async Task<Domain.Entities.Booking?> GetByIdTrackingWithIncludesAsync(Guid id, Guid? companyId = null)
    {
        var query = GetFilteredQuery(companyId: companyId)
            .AsTracking()
            .Include(b => b.Payments)
            .Include(b => b.Reviews);

        return await query.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Domain.Entities.Booking?> GetByIdTrackingWithPaymentsAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Domain.Entities.Booking?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Domain.Entities.Booking?> GetByIdForPilotAsync(Guid bookingId, Guid pilotId)
    {
        return await GetFilteredQuery()
            .Where(b => b.PilotId == pilotId)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    public async Task<IEnumerable<Domain.Entities.Booking>> GetByAircraftIdAsync(Guid aircraftId)
    {
        return await RepositoryDbSet
            .Where(b => b.AircraftId == aircraftId)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();
    }

    public void AddPayment(Payment payment)
    {
        RepositoryDbContext.Payments.Add(payment);
    }

    // ── Cross-module API support ──

    public async Task<int> CountByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        return await RepositoryDbSet.CountAsync(b => b.CompanyId == companyId, ct);
    }

    public async Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await RepositoryDbSet.CountAsync(b => b.PilotId == userId, ct);
    }

    public async Task<int> CountAllAsync(CancellationToken ct = default)
    {
        return await RepositoryDbSet.CountAsync(ct);
    }
}
