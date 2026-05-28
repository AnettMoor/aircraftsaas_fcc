using MediatR;
using Shared.Contracts.Users.DTOs;

namespace Users.Application.InternalQueries;

internal record GetCompaniesByIdsInternalQuery(IEnumerable<Guid> CompanyIds) : IRequest<Dictionary<Guid, CompanyBasicDto>>;
