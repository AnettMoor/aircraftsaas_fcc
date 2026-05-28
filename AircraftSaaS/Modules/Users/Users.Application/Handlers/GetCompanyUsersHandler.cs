using MediatR;
using Shared.Contracts.Users.DTOs;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class GetCompanyUsersHandler : IRequestHandler<GetCompanyUsersInternalQuery, List<UserBasicDto>>
{
    private readonly IUsersUOW _uow;

    public GetCompanyUsersHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<List<UserBasicDto>> Handle(GetCompanyUsersInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.CompanyRepository.GetCompanyUserBasicsAsync(request.CompanyId, cancellationToken);
    }
}
