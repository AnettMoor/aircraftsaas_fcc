using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;

namespace Fleet.Application.Handlers;

internal sealed class GetTotalAirportsCountHandler(IFleetUOW uow)
    : IRequestHandler<GetTotalAirportsCountInternalQuery, int>
{
    public async Task<int> Handle(GetTotalAirportsCountInternalQuery request, CancellationToken cancellationToken)
    {
        return await uow.AirportRepository.CountAllAsync(cancellationToken);
    }
}
