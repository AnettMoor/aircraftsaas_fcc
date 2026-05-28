using Fleet.Application.Contracts;
using Fleet.Infrastructure.Repositories;
using Shared.Kernel.DAL;

namespace Fleet.Infrastructure;

internal sealed class FleetUOW : BaseUOW<FleetDbContext>, IFleetUOW
{
    // Lazy-initialized repository backing fields
    private IAircraftRepository? _aircraftRepository;
    private IAircraftAvailabilityRepository? _aircraftAvailabilityRepository;
    private IAirportRepository? _airportRepository;
    private IInsurancePolicyRepository? _insurancePolicyRepository;
    private IMaintenanceRecordRepository? _maintenanceRecordRepository;

    public FleetUOW(FleetDbContext dbContext) : base(dbContext)
    {
    }

    public IAircraftRepository AircraftRepository =>
        _aircraftRepository ??= new AircraftRepository(UowDbContext);

    public IAircraftAvailabilityRepository AircraftAvailabilityRepository =>
        _aircraftAvailabilityRepository ??= new AircraftAvailabilityRepository(UowDbContext);

    public IAirportRepository AirportRepository =>
        _airportRepository ??= new AirportRepository(UowDbContext);

    public IInsurancePolicyRepository InsurancePolicyRepository =>
        _insurancePolicyRepository ??= new InsurancePolicyRepository(UowDbContext);

    public IMaintenanceRecordRepository MaintenanceRecordRepository =>
        _maintenanceRecordRepository ??= new MaintenanceRecordRepository(UowDbContext);
}
