using Base.DAL.Contracts;

namespace App.Domain.Contracts;

public interface IAppUOW : IBaseUOW
{
    IAircraftRepository AircraftRepository { get; }
    IBookingRepository BookingRepository { get; }
    ICompanyRepository CompanyRepository { get; }
    IAirportRepository AirportRepository { get; }
    IReviewRepository ReviewRepository { get; }
    IMaintenanceRecordRepository MaintenanceRecordRepository { get; }
    IAuditLogRepository AuditLogRepository { get; }
    IInsurancePolicyRepository InsurancePolicyRepository { get; }
    IAircraftAvailabilityRepository AircraftAvailabilityRepository { get; }
    ILicenseRepository LicenseRepository { get; }
    IPersonRepository PersonRepository { get; }
    IContactTypeRepository ContactTypeRepository { get; }
    IContactRepository ContactRepository { get; }
    IAppUserCompanyRepository AppUserCompanyRepository { get; }
}
