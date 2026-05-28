using MediatR;

namespace Fleet.Application.InternalCommands;

internal record BlockAircraftAvailabilityInternalCommand(
    Guid AircraftId,
    Guid? BookingId,
    DateTime StartDateTime,
    DateTime EndDateTime,
    string AvailabilityType,
    string? Reason) : IRequest<Guid>;
