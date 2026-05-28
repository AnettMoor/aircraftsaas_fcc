using MediatR;
using Shared.Contracts.Users.DTOs;

namespace Users.Application.InternalQueries;

internal record GetCompanyUsersInternalQuery(Guid CompanyId) : IRequest<List<UserBasicDto>>;
