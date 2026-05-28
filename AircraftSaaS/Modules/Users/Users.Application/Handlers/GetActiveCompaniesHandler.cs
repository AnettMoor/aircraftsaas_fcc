using MediatR;
using Shared.Contracts.Common;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class GetActiveCompaniesHandler : IRequestHandler<GetActiveCompaniesInternalQuery, List<CompanySelectItemDto>>
{
    private readonly IUsersUOW _uow;

    public GetActiveCompaniesHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<List<CompanySelectItemDto>> Handle(GetActiveCompaniesInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.CompanyRepository.GetActiveSelectItemsAsync(cancellationToken);
    }
}
