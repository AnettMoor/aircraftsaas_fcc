using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;

namespace Fleet.Application.Handlers;

internal sealed class GetTotalAircraftCountHandler(IFleetUOW uow)
    : IRequestHandler<GetTotalAircraftCountInternalQuery, int>
{
    public async Task<int> Handle(GetTotalAircraftCountInternalQuery request, CancellationToken cancellationToken)
    {
        return await uow.AircraftRepository.CountAllAsync(cancellationToken);
    }
}
