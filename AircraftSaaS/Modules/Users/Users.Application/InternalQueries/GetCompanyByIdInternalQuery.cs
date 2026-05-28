using MediatR;
using Shared.Contracts.Users.DTOs;

namespace Users.Application.InternalQueries;

internal record GetCompanyByIdInternalQuery(Guid CompanyId) : IRequest<CompanyBasicDto?>;
