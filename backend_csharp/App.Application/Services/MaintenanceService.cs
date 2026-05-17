using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain.Entities;
using Base.Domain;
using Microsoft.Extensions.Logging;

namespace App.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IAppUOW _uow;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(IAppUOW uow, ILogger<MaintenanceService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<IEnumerable<MaintenanceRecordDto>> GetAllForCompanyAsync(Guid companyId, Guid? aircraftId = null)
    {
        var records = await _uow.MaintenanceRecordRepository.GetAllForCompanyAsync(companyId, aircraftId);
        return records.Select(MapToDto);
    }

    public async Task<MaintenanceRecordDto?> GetByIdAsync(Guid id, Guid companyId)
    {
        var record = await _uow.MaintenanceRecordRepository.GetByIdForCompanyAsync(id, companyId);
        return record == null ? null : MapToDto(record);
    }

    public async Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordDto dto, Guid companyId, string createdBy)
    {
        // Verify aircraft exists for the company
        var aircraft = await _uow.AircraftRepository.GetByIdForCompanyTrackingAsync(dto.AircraftId, companyId);
        if (aircraft == null)
            throw new InvalidOperationException("Aircraft not found or does not belong to your company.");

        // Validate start/end date range
        if (dto.StartDate.HasValue && dto.EndDate.HasValue && dto.StartDate.Value >= dto.EndDate.Value)
            throw new InvalidOperationException("Start date must be before end date.");

        var record = new MaintenanceRecord
        {
            AircraftId = dto.AircraftId,
            MaintenanceDate = DateTime.SpecifyKind(dto.MaintenanceDate, DateTimeKind.Utc),
            StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc) : null,
            EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc) : null,
            MaintenanceType = new LangStr(dto.MaintenanceType),
            Status = new LangStr(dto.IsCompleted ? "Completed" : "Scheduled"),
            Description = new LangStr(dto.Description ?? ""),
            PerformedBy = dto.PerformedBy ?? "",
            AirframeHoursAtMaintenance = dto.AirframeHoursAtMaintenance,
            NextDueDate = dto.NextDueDate.HasValue ? DateTime.SpecifyKind(dto.NextDueDate.Value, DateTimeKind.Utc) : null,
            NextDueHours = dto.NextDueHours,
            Cost = dto.Cost,
            IsCompleted = dto.IsCompleted,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _uow.MaintenanceRecordRepository.Add(record);
        await _uow.SaveChangesAsync();

        // Create an AircraftAvailability block for the maintenance timeframe
        if (dto.StartDate.HasValue && dto.EndDate.HasValue)
        {
            await CreateMaintenanceAvailabilityBlockAsync(record);
        }

        // Reload with Aircraft include
        var created = await _uow.MaintenanceRecordRepository.GetByIdForCompanyAsync(record.Id, companyId);

        _logger.LogInformation("Maintenance record {Id} created for aircraft {AircraftId} by {CreatedBy}",
            record.Id, record.AircraftId, createdBy);

        return MapToDto(created ?? record);
    }

    public async Task<MaintenanceRecordDto> UpdateAsync(Guid id, UpdateMaintenanceRecordDto dto, Guid companyId, string updatedBy)
    {
        var record = await _uow.MaintenanceRecordRepository.GetByIdForCompanyTrackingAsync(id, companyId);

        if (record == null)
            throw new InvalidOperationException($"Maintenance record with id {id} not found.");

        // Validate start/end date range
        if (dto.StartDate.HasValue && dto.EndDate.HasValue && dto.StartDate.Value >= dto.EndDate.Value)
            throw new InvalidOperationException("Start date must be before end date.");

        // Remove old availability block if one existed
        await RemoveMaintenanceAvailabilityBlockAsync(record);

        record.MaintenanceDate = DateTime.SpecifyKind(dto.MaintenanceDate, DateTimeKind.Utc);
        record.StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc) : null;
        record.EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc) : null;
        record.MaintenanceType.SetTranslation(dto.MaintenanceType);
        record.Status.SetTranslation(dto.IsCompleted ? "Completed" : "Scheduled");
        record.Description.SetTranslation(dto.Description ?? "");
        record.PerformedBy = dto.PerformedBy ?? "";
        record.AirframeHoursAtMaintenance = dto.AirframeHoursAtMaintenance;
        record.NextDueDate = dto.NextDueDate.HasValue
            ? DateTime.SpecifyKind(dto.NextDueDate.Value, DateTimeKind.Utc)
            : null;
        record.NextDueHours = dto.NextDueHours;
        record.Cost = dto.Cost;
        record.IsCompleted = dto.IsCompleted;
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = updatedBy;

        await _uow.SaveChangesAsync();

        // Create new availability block if dates are set
        if (dto.StartDate.HasValue && dto.EndDate.HasValue)
        {
            await CreateMaintenanceAvailabilityBlockAsync(record);
        }

        _logger.LogInformation("Maintenance record {Id} updated by {UpdatedBy}", id, updatedBy);

        return MapToDto(record);
    }

    public async Task DeleteAsync(Guid id, Guid companyId, string deletedBy)
    {
        var record = await _uow.MaintenanceRecordRepository.GetByIdForCompanyTrackingAsync(id, companyId);

        if (record == null)
            throw new InvalidOperationException($"Maintenance record with id {id} not found.");

        // Remove the associated availability block
        await RemoveMaintenanceAvailabilityBlockAsync(record);

        record.SoftDelete(deletedBy);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Maintenance record {Id} soft-deleted by {DeletedBy}", id, deletedBy);
    }

    public async Task RestoreAsync(Guid id, Guid companyId)
    {
        var record = await _uow.MaintenanceRecordRepository.GetDeletedByIdForCompanyTrackingAsync(id, companyId);

        if (record == null)
            throw new InvalidOperationException($"Maintenance record with id {id} not found or is not deleted.");

        record.Restore();
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Maintenance record {Id} restored", id);
    }

    /// <summary>
    /// Creates an AircraftAvailability block of type "Maintenance" for the given maintenance record.
    /// </summary>
    private async Task CreateMaintenanceAvailabilityBlockAsync(MaintenanceRecord record)
    {
        if (!record.StartDate.HasValue || !record.EndDate.HasValue) return;

        var availability = new AircraftAvailability
        {
            AircraftId = record.AircraftId,
            StartDateTime = record.StartDate.Value,
            EndDateTime = record.EndDate.Value,
            AvailabilityType = "Maintenance",
            Reason = $"Maintenance: {record.MaintenanceType} (Record: {record.Id})"
        };

        _uow.AircraftAvailabilityRepository.Add(availability);
        await _uow.SaveChangesAsync();

        _logger.LogInformation(
            "Created Maintenance availability block {AvailId} for aircraft {AircraftId} from {Start} to {End}",
            availability.Id, record.AircraftId, record.StartDate, record.EndDate);
    }

    /// <summary>
    /// Removes any existing AircraftAvailability blocks that match this maintenance record.
    /// </summary>
    private async Task RemoveMaintenanceAvailabilityBlockAsync(MaintenanceRecord record)
    {
        // Find availability blocks matching this maintenance record by reason tag
        var allAvailabilities = await _uow.AircraftAvailabilityRepository.GetAllForAircraftAsync(record.AircraftId);
        var matchingTag = $"Record: {record.Id}";

        foreach (var avail in allAvailabilities)
        {
            if (avail.AvailabilityType == "Maintenance" &&
                avail.Reason != null &&
                avail.Reason.Contains(matchingTag))
            {
                var tracked = await _uow.AircraftAvailabilityRepository.GetByIdForAircraftTrackingAsync(avail.Id, record.AircraftId);
                if (tracked != null)
                {
                    tracked.SoftDelete("system");
                }
            }
        }

        await _uow.SaveChangesAsync();
    }

    private static MaintenanceRecordDto MapToDto(MaintenanceRecord record) => new()
    {
        Id = record.Id,
        AircraftId = record.AircraftId,
        AircraftName = record.Aircraft != null
            ? $"{record.Aircraft.Make} {record.Aircraft.Model} ({record.Aircraft.RegistrationNumber})"
            : "",
        MaintenanceDate = record.MaintenanceDate,
        StartDate = record.StartDate,
        EndDate = record.EndDate,
        MaintenanceType = record.MaintenanceType.ToString(),
        Status = record.Status.ToString(),
        Description = record.Description.ToString(),
        PerformedBy = record.PerformedBy,
        AirframeHoursAtMaintenance = record.AirframeHoursAtMaintenance,
        NextDueDate = record.NextDueDate,
        NextDueHours = record.NextDueHours,
        Cost = record.Cost,
        IsCompleted = record.IsCompleted,
        CreatedAt = record.CreatedAt
    };
}
