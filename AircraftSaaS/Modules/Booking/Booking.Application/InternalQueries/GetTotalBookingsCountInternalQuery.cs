using MediatR;

namespace Booking.Application.InternalQueries;

internal record GetTotalBookingsCountInternalQuery() : IRequest<int>;
