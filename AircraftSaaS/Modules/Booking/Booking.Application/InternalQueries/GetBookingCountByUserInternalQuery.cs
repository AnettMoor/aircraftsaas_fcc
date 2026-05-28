using MediatR;

namespace Booking.Application.InternalQueries;

internal record GetBookingCountByUserInternalQuery(Guid UserId) : IRequest<int>;
