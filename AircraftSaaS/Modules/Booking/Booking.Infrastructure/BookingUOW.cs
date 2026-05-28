using Booking.Application.Contracts;
using Booking.Infrastructure.Repositories;
using Shared.Kernel.DAL;

namespace Booking.Infrastructure;

internal sealed class BookingUOW : BaseUOW<BookingDbContext>, IBookingUOW
{
    // Lazy-initialized repository backing fields
    private IBookingRepository? _bookingRepository;
    private IReviewRepository? _reviewRepository;

    public BookingUOW(BookingDbContext dbContext) : base(dbContext)
    {
    }

    public IBookingRepository BookingRepository =>
        _bookingRepository ??= new BookingRepository(UowDbContext);

    public IReviewRepository ReviewRepository =>
        _reviewRepository ??= new ReviewRepository(UowDbContext);
}
