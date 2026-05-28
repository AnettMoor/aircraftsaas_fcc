using Booking.Application.Contracts;
using Booking.Application.InternalQueries;
using MediatR;

namespace Booking.Application.Handlers;

internal sealed class GetBookingCountByCompanyHandler(IBookingUOW uow)
    : IRequestHandler<GetBookingCountByCompanyInternalQuery, int>
{
    public async Task<int> Handle(GetBookingCountByCompanyInternalQuery request, CancellationToken cancellationToken)
    {
        return await uow.BookingRepository.CountByCompanyAsync(request.CompanyId, cancellationToken);
    }
}
