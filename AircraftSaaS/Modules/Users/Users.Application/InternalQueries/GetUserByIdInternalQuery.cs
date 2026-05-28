using MediatR;
using Shared.Contracts.Users.DTOs;

namespace Users.Application.InternalQueries;

internal record GetUserByIdInternalQuery(Guid UserId) : IRequest<UserBasicDto?>;
