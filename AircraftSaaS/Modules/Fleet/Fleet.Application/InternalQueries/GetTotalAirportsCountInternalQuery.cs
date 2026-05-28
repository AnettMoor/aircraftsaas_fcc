using MediatR;

namespace Fleet.Application.InternalQueries;

internal record GetTotalAirportsCountInternalQuery() : IRequest<int>;
