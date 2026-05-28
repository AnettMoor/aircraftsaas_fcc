using MediatR;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Users.Application.Handlers;

internal sealed class CheckUserLicenseHandler : IRequestHandler<CheckUserLicenseInternalQuery, bool>
{
    private readonly IUsersUOW _uow;

    public CheckUserLicenseHandler(IUsersUOW uow)
    {
        _uow = uow;
    }

    public async Task<bool> Handle(CheckUserLicenseInternalQuery request, CancellationToken cancellationToken)
    {
        return await _uow.LicenseRepository.HasValidLicenseForTypeAsync(
            request.UserId,
            request.RequiredLicenseType,
            request.AsOfDate);
    }
}
