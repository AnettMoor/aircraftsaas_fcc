using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.Application.Handlers;

internal sealed class GetAircraftsByIdsHandler : IRequestHandler<GetAircraftsByIdsInternalQuery, Dictionary<Guid, AircraftBasicDto>>
{
    private readonly IFleetUOW _uow;

    public GetAircraftsByIdsHandler(IFleetUOW uow)
    {
        _uow = uow;
    }

    public async Task<Dictionary<Guid, AircraftBasicDto>> Handle(GetAircraftsByIdsInternalQuery request, CancellationToken cancellationToken)
    {
        var aircrafts = await _uow.AircraftRepository.GetByIdsAsync(request.AircraftIds, cancellationToken);

        return aircrafts.ToDictionary(
            a => a.Id,
            a => new AircraftBasicDto(
                a.Id,
                a.RegistrationNumber,
                a.Model.ToString(),
                a.CompanyId,
                a.RequiredLicenseType));
    }
}
