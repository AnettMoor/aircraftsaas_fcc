using Booking.Application.Contracts;
using Booking.Application.InternalQueries;
using MediatR;

namespace Booking.Application.Handlers;

internal sealed class GetBookingCountByUserHandler(IBookingUOW uow)
    : IRequestHandler<GetBookingCountByUserInternalQuery, int>
{
    public async Task<int> Handle(GetBookingCountByUserInternalQuery request, CancellationToken cancellationToken)
    {
        return await uow.BookingRepository.CountByUserAsync(request.UserId, cancellationToken);
    }
}
