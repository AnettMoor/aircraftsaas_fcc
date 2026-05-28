using MediatR;

namespace Users.Application.InternalQueries;

internal record CheckUserLicenseInternalQuery(
    Guid UserId,
    string RequiredLicenseType,
    DateTime AsOfDate) : IRequest<bool>;
