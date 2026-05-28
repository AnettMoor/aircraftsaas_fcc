using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.Application.Handlers;

internal sealed class GetAircraftsByCompanyHandler : IRequestHandler<GetAircraftsByCompanyInternalQuery, List<AircraftBasicDto>>
{
    private readonly IFleetUOW _uow;

    public GetAircraftsByCompanyHandler(IFleetUOW uow)
    {
        _uow = uow;
    }

    public async Task<List<AircraftBasicDto>> Handle(GetAircraftsByCompanyInternalQuery request, CancellationToken cancellationToken)
    {
        var aircraftList = await _uow.AircraftRepository.GetAllForCompanyAsync(request.CompanyId);

        return aircraftList.Select(a => new AircraftBasicDto(
            a.Id,
            a.RegistrationNumber,
            a.Model.ToString(),
            a.CompanyId,
            a.RequiredLicenseType)).ToList();
    }
}
