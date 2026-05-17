using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class ReviewRepository : BaseRepository<Review, Review, AppDbContext>, IReviewRepository
{
    public ReviewRepository(AppDbContext dbContext, IBaseMapper<Review, Review> mapper)
        : base(dbContext, mapper)
    {
    }

    public ReviewRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<Review>())
    {
    }

    public async Task<IEnumerable<Review>> GetAllWithIncludesAsync()
    {
        // Public listing — no IDOR filter (intentionally shows all reviews)
        return await RepositoryDbSet
            .Include(r => r.Aircraft)
            .Include(r => r.Author)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetByAircraftIdAsync(Guid aircraftId)
    {
        // Public listing — no IDOR filter
        return await RepositoryDbSet
            .Include(r => r.Aircraft)
            .Include(r => r.Author)
            .Where(r => r.AircraftId == aircraftId)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync();
    }

    public async Task<Review?> GetByIdWithIncludesAsync(Guid id)
    {
        return await RepositoryDbSet
            .Include(r => r.Aircraft)
            .Include(r => r.Author)
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
            .Include(r => r.Aircraft)
            .Include(r => r.Author)
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
