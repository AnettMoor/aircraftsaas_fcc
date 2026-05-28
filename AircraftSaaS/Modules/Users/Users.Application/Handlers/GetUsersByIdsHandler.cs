using MediatR;
using Shared.Contracts.Users.DTOs;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class GetUsersByIdsHandler : IRequestHandler<GetUsersByIdsInternalQuery, Dictionary<Guid, UserBasicDto>>
{
    private readonly IUsersUOW _uow;

    public GetUsersByIdsHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<Dictionary<Guid, UserBasicDto>> Handle(GetUsersByIdsInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.CompanyRepository.GetUserBasicsByIdsAsync(request.UserIds, cancellationToken);
    }
}
