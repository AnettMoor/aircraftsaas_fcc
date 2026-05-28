using Booking.Application.Contracts;
using Booking.Domain.Entities;
using Shared.Kernel.DAL;
using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Repositories;

/// <summary>
/// Review repository for the Booking module.
/// Cross-module includes (Aircraft, Author navigations) have been removed —
/// those are NO LONGER navigation properties in the modular domain.
/// Display names are resolved at the service layer via MediatR.
/// </summary>
internal sealed class ReviewRepository : BaseRepository<Review, Review, BookingDbContext>, IReviewRepository
{
    public ReviewRepository(BookingDbContext dbContext, IBaseMapper<Review, Review> mapper)
        : base(dbContext, mapper)
    {
    }

    public ReviewRepository(BookingDbContext dbContext)
        : base(dbContext, new BaseMapper<Review>())
    {
    }

    public async Task<IEnumerable<Review>> GetAllAsync()
    {
        return await RepositoryDbSet
            .Include(r => r.Booking)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetByAircraftIdAsync(Guid aircraftId)
    {
        return await RepositoryDbSet
            .Include(r => r.Booking)
            .Where(r => r.AircraftId == aircraftId)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync();
    }

    public async Task<Review?> GetByIdWithIncludesAsync(Guid id)
    {
        return await RepositoryDbSet
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Review?> GetByBookingIdAsync(Guid bookingId)
    {
        return await RepositoryDbSet.FirstOrDefaultAsync(r => r.BookingId == bookingId);
    }

    public async Task<Review?> GetByIdTrackingAsync(Guid id)
    {
        return await RepositoryDbSet
            .AsTracking()
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<double> GetAverageRatingForAircraftAsync(Guid aircraftId)
    {
        var reviews = await RepositoryDbSet
            .Where(r => r.AircraftId == aircraftId)
            .ToListAsync();

        return reviews.Any() ? reviews.Average(r => r.Rating) : 0;
    }
}
