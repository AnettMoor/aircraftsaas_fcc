using MediatR;
using Shared.Contracts.Users.DTOs;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class GetCompanyByIdHandler : IRequestHandler<GetCompanyByIdInternalQuery, CompanyBasicDto?>
{
    private readonly IUsersUOW _uow;

    public GetCompanyByIdHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<CompanyBasicDto?> Handle(GetCompanyByIdInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.CompanyRepository.GetBasicByIdAsync(request.CompanyId, cancellationToken);
    }
}
