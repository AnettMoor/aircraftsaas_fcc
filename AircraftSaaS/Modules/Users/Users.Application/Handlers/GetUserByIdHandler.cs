using MediatR;
using Shared.Contracts.Users.DTOs;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdInternalQuery, UserBasicDto?>
{
    private readonly IUsersUOW _uow;

    public GetUserByIdHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<UserBasicDto?> Handle(GetUserByIdInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.CompanyRepository.GetUserBasicByIdAsync(request.UserId, cancellationToken);
    }
}
