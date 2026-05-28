using Fleet.Application.Contracts;
using Fleet.Application.InternalCommands;
using Fleet.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Fleet.Application.Handlers;

/// <summary>
/// Creates an AircraftAvailability block for an aircraft (e.g., when a booking is confirmed).
/// Returns the ID of the created availability record.
/// </summary>
internal sealed class BlockAircraftAvailabilityHandler : IRequestHandler<BlockAircraftAvailabilityInternalCommand, Guid>
{
    private readonly IFleetUOW _uow;
    private readonly ILogger<BlockAircraftAvailabilityHandler> _logger;

    public BlockAircraftAvailabilityHandler(IFleetUOW uow, ILogger<BlockAircraftAvailabilityHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Guid> Handle(BlockAircraftAvailabilityInternalCommand request, CancellationToken cancellationToken)
    {
        var availability = new AircraftAvailability
        {
            AircraftId = request.AircraftId,
            BookingId = request.BookingId,
            StartDateTime = DateTime.SpecifyKind(request.StartDateTime, DateTimeKind.Utc),
            EndDateTime = DateTime.SpecifyKind(request.EndDateTime, DateTimeKind.Utc),
            AvailabilityType = request.AvailabilityType,
            Reason = request.Reason
        };

        _uow.AircraftAvailabilityRepository.Add(availability);
        await _uow.SaveChangesAsync();

        _logger.LogInformation(
            "Created availability block {Id} for aircraft {AircraftId}: {Type} from {Start} to {End} (BookingId: {BookingId})",
            availability.Id, request.AircraftId, request.AvailabilityType,
            request.StartDateTime, request.EndDateTime, request.BookingId);

        return availability.Id;
    }
}
