using MediatR;
using Shared.Contracts.Common;
using Shared.Contracts.Users;
using Shared.Contracts.Users.DTOs;
using Users.Application.InternalQueries;

namespace Users.Application;

internal sealed class UsersModuleApi : IUsersModuleApi
{
    private readonly IMediator _mediator;

    public UsersModuleApi(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<UserBasicDto?> GetUserByIdAsync(Guid userId, CancellationToken ct)
        => _mediator.Send(new GetUserByIdInternalQuery(userId), ct);

    public Task<Dictionary<Guid, UserBasicDto>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct)
        => _mediator.Send(new GetUsersByIdsInternalQuery(userIds), ct);

    public Task<bool> CheckUserLicenseAsync(Guid userId, string requiredLicenseType, DateTime asOfDate, CancellationToken ct)
        => _mediator.Send(new CheckUserLicenseInternalQuery(userId, requiredLicenseType, asOfDate), ct);

    public Task<CompanyBasicDto?> GetCompanyByIdAsync(Guid companyId, CancellationToken ct)
        => _mediator.Send(new GetCompanyByIdInternalQuery(companyId), ct);

    public Task<Dictionary<Guid, CompanyBasicDto>> GetCompaniesByIdsAsync(IEnumerable<Guid> companyIds, CancellationToken ct)
        => _mediator.Send(new GetCompaniesByIdsInternalQuery(companyIds), ct);

    public Task<List<UserBasicDto>> GetCompanyUsersAsync(Guid companyId, CancellationToken ct)
        => _mediator.Send(new GetCompanyUsersInternalQuery(companyId), ct);

    public Task<List<CompanySelectItemDto>> GetActiveCompaniesAsync(CancellationToken ct)
        => _mediator.Send(new GetActiveCompaniesInternalQuery(), ct);
}
