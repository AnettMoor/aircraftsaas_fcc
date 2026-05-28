using MediatR;
using Shared.Contracts.Fleet;
using Shared.Contracts.Fleet.DTOs;
using Fleet.Application.InternalQueries;
using Fleet.Application.InternalCommands;

namespace Fleet.Application;

internal sealed class FleetModuleApi : IFleetModuleApi
{
    private readonly IMediator _mediator;

    public FleetModuleApi(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<AircraftBasicDto?> GetAircraftByIdAsync(Guid aircraftId, CancellationToken ct)
        => _mediator.Send(new GetAircraftByIdInternalQuery(aircraftId), ct);

    public Task<Dictionary<Guid, AircraftBasicDto>> GetAircraftsByIdsAsync(IEnumerable<Guid> aircraftIds, CancellationToken ct)
        => _mediator.Send(new GetAircraftsByIdsInternalQuery(aircraftIds), ct);

    public Task<bool> CheckAircraftAvailabilityAsync(Guid aircraftId, DateTime startDateTime, DateTime endDateTime, CancellationToken ct)
        => _mediator.Send(new CheckAircraftAvailabilityInternalQuery(aircraftId, startDateTime, endDateTime), ct);

    public Task<int> GetAircraftCountByCompanyAsync(Guid companyId, CancellationToken ct)
        => _mediator.Send(new GetAircraftCountByCompanyInternalQuery(companyId), ct);

    public Task<List<AircraftBasicDto>> GetAircraftsByCompanyAsync(Guid companyId, CancellationToken ct)
        => _mediator.Send(new GetAircraftsByCompanyInternalQuery(companyId), ct);

    public Task<int> GetTotalAircraftCountAsync(CancellationToken ct)
        => _mediator.Send(new GetTotalAircraftCountInternalQuery(), ct);

    public Task<int> GetTotalAirportsCountAsync(CancellationToken ct)
        => _mediator.Send(new GetTotalAirportsCountInternalQuery(), ct);

    public Task<Guid> BlockAircraftAvailabilityAsync(Guid aircraftId, Guid? bookingId, DateTime startDateTime, DateTime endDateTime, string availabilityType, string? reason, CancellationToken ct)
        => _mediator.Send(new BlockAircraftAvailabilityInternalCommand(aircraftId, bookingId, startDateTime, endDateTime, availabilityType, reason), ct);
}
