using MediatR;
using Shared.Contracts.Common;

namespace Users.Application.InternalQueries;

internal record GetActiveCompaniesInternalQuery() : IRequest<List<CompanySelectItemDto>>;
