using MediatR;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.Application.InternalQueries;

internal record GetAircraftByIdInternalQuery(Guid AircraftId) : IRequest<AircraftBasicDto?>;
