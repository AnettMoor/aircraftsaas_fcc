using MediatR;
using Shared.Contracts.Booking;
using Shared.Contracts.Booking.DTOs;
using Booking.Application.InternalQueries;

namespace Booking.Application;

internal sealed class BookingModuleApi : IBookingModuleApi
{
    private readonly IMediator _mediator;

    public BookingModuleApi(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<int> GetBookingCountByCompanyAsync(Guid companyId, CancellationToken ct)
        => _mediator.Send(new GetBookingCountByCompanyInternalQuery(companyId), ct);

    public Task<int> GetBookingCountByUserAsync(Guid userId, CancellationToken ct)
        => _mediator.Send(new GetBookingCountByUserInternalQuery(userId), ct);

    public Task<List<BookingBasicDto>> GetBookingsByAircraftAsync(Guid aircraftId, CancellationToken ct)
        => _mediator.Send(new GetBookingsByAircraftInternalQuery(aircraftId), ct);

    public Task<int> GetTotalBookingsCountAsync(CancellationToken ct)
        => _mediator.Send(new GetTotalBookingsCountInternalQuery(), ct);
}
