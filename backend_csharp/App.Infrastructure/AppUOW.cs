using App.Domain.Contracts;
using App.Infrastructure.Repositories;
using Base.DAL.EF;

namespace App.Infrastructure;

public class AppUOW : BaseUOW<AppDbContext>, IAppUOW
{
    private IAircraftRepository? _aircraftRepository;
    private IBookingRepository? _bookingRepository;
    private ICompanyRepository? _companyRepository;
    private IAirportRepository? _airportRepository;
    private IReviewRepository? _reviewRepository;
    private IMaintenanceRecordRepository? _maintenanceRecordRepository;
    private IAuditLogRepository? _auditLogRepository;
    private IInsurancePolicyRepository? _insurancePolicyRepository;
    private IAircraftAvailabilityRepository? _aircraftAvailabilityRepository;
    private ILicenseRepository? _licenseRepository;
    private IPersonRepository? _personRepository;
    private IContactTypeRepository? _contactTypeRepository;
    private IContactRepository? _contactRepository;
    private IAppUserCompanyRepository? _appUserCompanyRepository;

    public AppUOW(AppDbContext dbContext) : base(dbContext)
    {
    }

    public IAircraftRepository AircraftRepository =>
        _aircraftRepository ??= new AircraftRepository(UowDbContext);

    public IBookingRepository BookingRepository =>
        _bookingRepository ??= new BookingRepository(UowDbContext);

    public ICompanyRepository CompanyRepository =>
        _companyRepository ??= new CompanyRepository(UowDbContext);

    public IAirportRepository AirportRepository =>
        _airportRepository ??= new AirportRepository(UowDbContext);

    public IReviewRepository ReviewRepository =>
        _reviewRepository ??= new ReviewRepository(UowDbContext);

    public IMaintenanceRecordRepository MaintenanceRecordRepository =>
        _maintenanceRecordRepository ??= new MaintenanceRecordRepository(UowDbContext);

    public IAuditLogRepository AuditLogRepository =>
        _auditLogRepository ??= new AuditLogRepository(UowDbContext);

    public IInsurancePolicyRepository InsurancePolicyRepository =>
        _insurancePolicyRepository ??= new InsurancePolicyRepository(UowDbContext);

    public IAircraftAvailabilityRepository AircraftAvailabilityRepository =>
        _aircraftAvailabilityRepository ??= new AircraftAvailabilityRepository(UowDbContext);

    public ILicenseRepository LicenseRepository =>
        _licenseRepository ??= new LicenseRepository(UowDbContext);

    public IPersonRepository PersonRepository =>
        _personRepository ??= new PersonRepository(UowDbContext);

    public IContactTypeRepository ContactTypeRepository =>
        _contactTypeRepository ??= new ContactTypeRepository(UowDbContext);

    public IContactRepository ContactRepository =>
        _contactRepository ??= new ContactRepository(UowDbContext);

    public IAppUserCompanyRepository AppUserCompanyRepository =>
        _appUserCompanyRepository ??= new AppUserCompanyRepository(UowDbContext);
}
