using MediatR;

namespace Fleet.Application.InternalQueries;

internal record CheckAircraftAvailabilityInternalQuery(
    Guid AircraftId,
    DateTime StartDateTime,
    DateTime EndDateTime) : IRequest<bool>;
