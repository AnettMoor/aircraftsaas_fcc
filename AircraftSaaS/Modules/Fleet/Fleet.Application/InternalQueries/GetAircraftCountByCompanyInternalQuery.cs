using MediatR;

namespace Fleet.Application.InternalQueries;

internal record GetAircraftCountByCompanyInternalQuery(Guid CompanyId) : IRequest<int>;
