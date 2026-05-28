using Shared.Kernel.DAL;

namespace Booking.Application.Contracts;

/// <summary>
/// Unit of Work for the Booking module.
/// Exposes ONLY Booking-module repositories.
/// Payment is accessed via the Booking navigation property.
/// </summary>
public interface IBookingUOW : IBaseUOW
{
    IBookingRepository BookingRepository { get; }
    IReviewRepository ReviewRepository { get; }
}
