using Booking.Application.Contracts;
using Booking.Application.InternalQueries;
using MediatR;
using Shared.Contracts.Booking.DTOs;

namespace Booking.Application.Handlers;

internal sealed class GetBookingsByAircraftHandler : IRequestHandler<GetBookingsByAircraftInternalQuery, List<BookingBasicDto>>
{
    private readonly IBookingUOW _uow;

    public GetBookingsByAircraftHandler(IBookingUOW uow)
    {
        _uow = uow;
    }

    public async Task<List<BookingBasicDto>> Handle(GetBookingsByAircraftInternalQuery request, CancellationToken cancellationToken)
    {
        var bookings = await _uow.BookingRepository.GetByAircraftIdAsync(request.AircraftId);

        return bookings.Select(b => new BookingBasicDto(
            b.Id,
            b.AircraftId,
            b.PilotId,
            b.StartDateTime,
            b.EndDateTime,
            b.Status.ToString()
        )).ToList();
    }
}
