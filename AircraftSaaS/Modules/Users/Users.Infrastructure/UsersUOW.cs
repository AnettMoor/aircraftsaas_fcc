using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Infrastructure.Repositories;

namespace Users.Infrastructure;

internal sealed class UsersUOW : BaseUOW<UsersDbContext>, IUsersUOW
{
    private ICompanyRepository? _companyRepository;
    private IPersonRepository? _personRepository;
    private IContactRepository? _contactRepository;
    private IContactTypeRepository? _contactTypeRepository;
    private ILicenseRepository? _licenseRepository;
    private IAuditLogRepository? _auditLogRepository;
    private IAppUserCompanyRepository? _appUserCompanyRepository;

    public UsersUOW(UsersDbContext dbContext) : base(dbContext)
    {
    }

    public ICompanyRepository CompanyRepository =>
        _companyRepository ??= new CompanyRepository(UowDbContext);

    public IPersonRepository PersonRepository =>
        _personRepository ??= new PersonRepository(UowDbContext);

    public IContactRepository ContactRepository =>
        _contactRepository ??= new ContactRepository(UowDbContext);

    public IContactTypeRepository ContactTypeRepository =>
        _contactTypeRepository ??= new ContactTypeRepository(UowDbContext);

    public ILicenseRepository LicenseRepository =>
        _licenseRepository ??= new LicenseRepository(UowDbContext);

    public IAuditLogRepository AuditLogRepository =>
        _auditLogRepository ??= new AuditLogRepository(UowDbContext);

    public IAppUserCompanyRepository AppUserCompanyRepository =>
        _appUserCompanyRepository ??= new AppUserCompanyRepository(UowDbContext);
}
