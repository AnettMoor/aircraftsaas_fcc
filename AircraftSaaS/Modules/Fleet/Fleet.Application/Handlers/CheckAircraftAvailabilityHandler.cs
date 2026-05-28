using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;

namespace Fleet.Application.Handlers;

/// <summary>
/// Checks if an aircraft is available (no blocking availability records) for the given time range.
/// Returns true if the aircraft IS available (no conflicts), false otherwise.
/// </summary>
internal sealed class CheckAircraftAvailabilityHandler : IRequestHandler<CheckAircraftAvailabilityInternalQuery, bool>
{
    private readonly IFleetUOW _uow;

    public CheckAircraftAvailabilityHandler(IFleetUOW uow)
    {
        _uow = uow;
    }

    public async Task<bool> Handle(CheckAircraftAvailabilityInternalQuery request, CancellationToken cancellationToken)
    {
        var hasBlocking = await _uow.AircraftAvailabilityRepository
            .HasBlockingAvailabilityAsync(request.AircraftId, request.StartDateTime, request.EndDateTime);

        // Return true if available (no blocking records)
        return !hasBlocking;
    }
}
