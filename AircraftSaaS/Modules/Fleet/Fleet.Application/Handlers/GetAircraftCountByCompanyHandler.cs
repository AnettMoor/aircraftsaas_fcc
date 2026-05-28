using Fleet.Application.Contracts;
using Fleet.Application.InternalQueries;
using MediatR;

namespace Fleet.Application.Handlers;

internal sealed class GetAircraftCountByCompanyHandler(IFleetUOW uow)
    : IRequestHandler<GetAircraftCountByCompanyInternalQuery, int>
{
    public async Task<int> Handle(GetAircraftCountByCompanyInternalQuery request, CancellationToken cancellationToken)
    {
        return await uow.AircraftRepository.GetCountForCompanyAsync(request.CompanyId);
    }
}
