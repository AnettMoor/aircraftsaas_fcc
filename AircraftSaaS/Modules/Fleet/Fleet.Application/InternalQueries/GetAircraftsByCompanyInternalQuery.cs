using MediatR;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.Application.InternalQueries;

internal record GetAircraftsByCompanyInternalQuery(Guid CompanyId) : IRequest<List<AircraftBasicDto>>;
