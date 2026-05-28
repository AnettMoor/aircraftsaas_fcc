using MediatR;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.Application.InternalQueries;

internal record GetAircraftsByIdsInternalQuery(IEnumerable<Guid> AircraftIds) : IRequest<Dictionary<Guid, AircraftBasicDto>>;
