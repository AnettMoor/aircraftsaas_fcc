using MediatR;

namespace Fleet.Application.InternalQueries;

internal record GetTotalAircraftCountInternalQuery() : IRequest<int>;
