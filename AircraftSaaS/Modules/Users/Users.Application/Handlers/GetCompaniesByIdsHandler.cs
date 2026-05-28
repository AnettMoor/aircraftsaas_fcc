using MediatR;
using Shared.Contracts.Users.DTOs;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class GetCompaniesByIdsHandler : IRequestHandler<GetCompaniesByIdsInternalQuery, Dictionary<Guid, CompanyBasicDto>>
{
    private readonly IUsersUOW _uow;

    public GetCompaniesByIdsHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<Dictionary<Guid, CompanyBasicDto>> Handle(GetCompaniesByIdsInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.CompanyRepository.GetBasicsByIdsAsync(request.CompanyIds, cancellationToken);
    }
}
