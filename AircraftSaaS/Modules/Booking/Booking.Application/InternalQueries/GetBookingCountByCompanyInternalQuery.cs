using MediatR;

namespace Booking.Application.InternalQueries;

internal record GetBookingCountByCompanyInternalQuery(Guid CompanyId) : IRequest<int>;
