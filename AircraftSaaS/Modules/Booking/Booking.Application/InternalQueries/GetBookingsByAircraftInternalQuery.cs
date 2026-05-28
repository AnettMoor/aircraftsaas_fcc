using MediatR;
using Shared.Contracts.Booking.DTOs;

namespace Booking.Application.InternalQueries;

internal record GetBookingsByAircraftInternalQuery(Guid AircraftId) : IRequest<List<BookingBasicDto>>;
