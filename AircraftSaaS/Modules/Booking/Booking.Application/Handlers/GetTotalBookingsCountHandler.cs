using Booking.Application.Contracts;
using Booking.Application.InternalQueries;
using MediatR;

namespace Booking.Application.Handlers;

internal sealed class GetTotalBookingsCountHandler(IBookingUOW uow)
    : IRequestHandler<GetTotalBookingsCountInternalQuery, int>
{
    public async Task<int> Handle(GetTotalBookingsCountInternalQuery request, CancellationToken cancellationToken)
    {
        return await uow.BookingRepository.CountAllAsync(cancellationToken);
    }
}
