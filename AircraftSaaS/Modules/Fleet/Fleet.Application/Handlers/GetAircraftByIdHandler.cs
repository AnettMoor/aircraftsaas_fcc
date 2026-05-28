using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.Application.Handlers;

internal sealed class GetAircraftByIdHandler : IRequestHandler<GetAircraftByIdInternalQuery, AircraftBasicDto?>
{
    private readonly IFleetUOW _uow;

    public GetAircraftByIdHandler(IFleetUOW uow)
    {
        _uow = uow;
    }

    public async Task<AircraftBasicDto?> Handle(GetAircraftByIdInternalQuery request, CancellationToken cancellationToken)
    {
        var aircraft = await _uow.AircraftRepository.FindAsync(request.AircraftId);
        if (aircraft == null)
            return null;

        return new AircraftBasicDto(
            aircraft.Id,
            aircraft.RegistrationNumber,
            aircraft.Model.ToString(),
            aircraft.CompanyId,
            aircraft.RequiredLicenseType);
    }
}
