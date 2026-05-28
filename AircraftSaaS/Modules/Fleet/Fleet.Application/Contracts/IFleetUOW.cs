using Shared.Kernel.DAL;

namespace Fleet.Application.Contracts;

/// <summary>
/// Unit of Work for the Fleet module — exposes ONLY Fleet-owned repositories.
/// </summary>
public interface IFleetUOW : IBaseUOW
{
    IAircraftRepository AircraftRepository { get; }
    IAircraftAvailabilityRepository AircraftAvailabilityRepository { get; }
    IAirportRepository AirportRepository { get; }
    IInsurancePolicyRepository InsurancePolicyRepository { get; }
    IMaintenanceRecordRepository MaintenanceRecordRepository { get; }
}
