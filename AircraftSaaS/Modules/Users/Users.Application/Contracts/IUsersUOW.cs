using Shared.Kernel.DAL;

namespace Users.Application.Contracts;

public interface IUsersUOW : IBaseUOW
{
    ICompanyRepository CompanyRepository { get; }
    IPersonRepository PersonRepository { get; }
    IContactRepository ContactRepository { get; }
    IContactTypeRepository ContactTypeRepository { get; }
    ILicenseRepository LicenseRepository { get; }
    IAuditLogRepository AuditLogRepository { get; }
    IAppUserCompanyRepository AppUserCompanyRepository { get; }
}
