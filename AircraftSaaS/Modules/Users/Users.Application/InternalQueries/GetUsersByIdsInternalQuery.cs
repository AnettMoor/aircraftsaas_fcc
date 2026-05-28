using MediatR;
using Shared.Contracts.Users.DTOs;

namespace Users.Application.InternalQueries;

internal record GetUsersByIdsInternalQuery(IEnumerable<Guid> UserIds) : IRequest<Dictionary<Guid, UserBasicDto>>;
