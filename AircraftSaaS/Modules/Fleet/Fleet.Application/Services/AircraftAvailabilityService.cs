using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Fleet.Application.Contracts;
using Fleet.Domain.Entities;

namespace Fleet.Application.Services;

internal sealed class AircraftAvailabilityService : IAircraftAvailabilityService
{
    private readonly IFleetUOW _uow;

    private static readonly string[] ValidAvailabilityTypes = { "Available", "Blocked", "Maintenance", "Booked" };

    public AircraftAvailabilityService(IFleetUOW uow)
    {
        _uow = uow;
    }

    public async Task<AircraftAvailabilityDto?> GetByIdAsync(Guid id, Guid aircraftId)
    {
        var availability = await _uow.AircraftAvailabilityRepository.GetByIdForAircraftAsync(id, aircraftId);
        return availability == null ? null : MapToDto(availability);
    }

    public async Task<IEnumerable<AircraftAvailabilityDto>> GetAllForAircraftAsync(Guid aircraftId)
    {
        var availabilities = await _uow.AircraftAvailabilityRepository.GetAllForAircraftAsync(aircraftId);
        var result = availabilities.Select(MapToDto).ToList();

        // Also include synthetic maintenance blocks from active maintenance records.
        // This ensures the calendar shows maintenance windows even if an explicit
        // AircraftAvailability row was never created for the maintenance record.
        var activeMaintenanceRecords = await _uow.MaintenanceRecordRepository.GetActiveForAircraftAsync(aircraftId);

        // Collect existing maintenance availability reason tags to avoid duplicates
        var existingReasonTags = new HashSet<string>(
            result
                .Where(a => a.AvailabilityType == "Maintenance" && a.Reason != null)
                .Select(a => a.Reason!));

        foreach (var record in activeMaintenanceRecords)
        {
            // Check if there is already an availability block for this maintenance record
            var tag = $"Record: {record.Id}";
            if (existingReasonTags.Any(r => r.Contains(tag)))
                continue;

            // Determine the maintenance time range.
            // If StartDate/EndDate are set, use those.
            // Otherwise fall back to MaintenanceDate as a single-day block.
            DateTime startDt;
            DateTime endDt;

            if (record.StartDate.HasValue && record.EndDate.HasValue)
            {
                startDt = record.StartDate.Value;
                endDt = record.EndDate.Value;
            }
            else
            {
                // Use MaintenanceDate as a full-day block
                startDt = record.MaintenanceDate.Date;
                endDt = record.MaintenanceDate.Date.AddDays(1);
            }

            // Synthesize a virtual availability block
            result.Add(new AircraftAvailabilityDto
            {
                Id = record.Id, // Use maintenance record ID as a stable identifier
                AircraftId = aircraftId,
                StartDateTime = startDt,
                EndDateTime = endDt,
                AvailabilityType = "Maintenance",
                Reason = $"Maintenance: {record.MaintenanceType} (Record: {record.Id})"
            });
        }

        // If the aircraft does not have an active insurance policy,
        // inject a synthetic "NoInsurance" block so that the calendar
        // (and any other API consumer) treats it as fully unavailable.
        var hasInsurance = await _uow.InsurancePolicyRepository.HasActivePolicyAsync(aircraftId);
        if (!hasInsurance)
        {
            result.Add(new AircraftAvailabilityDto
            {
                Id = Guid.Empty,
                AircraftId = aircraftId,
                StartDateTime = DateTime.UtcNow.AddYears(-1),
                EndDateTime = DateTime.UtcNow.AddYears(2),
                AvailabilityType = "NoInsurance",
                Reason = "Aircraft does not have active insurance coverage"
            });
        }

        return result.OrderBy(a => a.StartDateTime);
    }

    public async Task<AircraftAvailabilityDto> CreateAsync(CreateAircraftAvailabilityDto dto, Guid aircraftId, Guid companyId)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        ValidateDto(dto.StartDateTime, dto.EndDateTime, dto.AvailabilityType);

        var availability = new AircraftAvailability
        {
            AircraftId = aircraftId,
            StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc),
            EndDateTime = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc),
            AvailabilityType = dto.AvailabilityType,
            Reason = dto.Reason
        };

        _uow.AircraftAvailabilityRepository.Add(availability);
        await _uow.SaveChangesAsync();

        return MapToDto(availability);
    }

    public async Task<AircraftAvailabilityDto> UpdateAsync(Guid id, UpdateAircraftAvailabilityDto dto, Guid aircraftId, Guid companyId)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var availability = await _uow.AircraftAvailabilityRepository.GetByIdTrackingAsync(id);
        if (availability == null || availability.AircraftId != aircraftId)
            throw new InvalidOperationException("Availability record not found.");

        ValidateDto(dto.StartDateTime, dto.EndDateTime, dto.AvailabilityType);

        availability.StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc);
        availability.EndDateTime = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc);
        availability.AvailabilityType = dto.AvailabilityType;
        availability.Reason = dto.Reason;

        await _uow.SaveChangesAsync();

        return MapToDto(availability);
    }

    public async Task DeleteAsync(Guid id, Guid aircraftId, Guid companyId, string deletedBy)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var availability = await _uow.AircraftAvailabilityRepository.GetByIdTrackingAsync(id);
        if (availability == null || availability.AircraftId != aircraftId)
            throw new InvalidOperationException("Availability record not found.");

        availability.SoftDelete(deletedBy);
        await _uow.SaveChangesAsync();
    }

    private static void ValidateDto(DateTime start, DateTime end, string availabilityType)
    {
        if (start >= end)
            throw new InvalidOperationException("StartDateTime must be before EndDateTime.");

        if (!ValidAvailabilityTypes.Contains(availabilityType))
            throw new InvalidOperationException("AvailabilityType must be Available, Blocked, or Maintenance.");
    }

    private static AircraftAvailabilityDto MapToDto(AircraftAvailability availability) => new()
    {
        Id = availability.Id,
        AircraftId = availability.AircraftId,
        StartDateTime = availability.StartDateTime,
        EndDateTime = availability.EndDateTime,
        AvailabilityType = availability.AvailabilityType,
        Reason = availability.Reason
    };

    
    /// <summary>
    /// Merge overlapping or adjacent insurance periods into non-overlapping ranges.
    /// Input must be sorted by Start ascending.
    /// </summary>
    private static List<(DateTime Start, DateTime End)> MergeInsurancePeriods(List<(DateTime Start, DateTime End)> periods)
    {
        if (periods.Count == 0) return new List<(DateTime, DateTime)>();

        var merged = new List<(DateTime Start, DateTime End)>();
        var current = periods[0];

        for (var i = 1; i < periods.Count; i++)
        {
            if (periods[i].Start <= current.End)
            {
                // Overlapping or adjacent — extend the current period
                current.End = current.End > periods[i].End ? current.End : periods[i].End;
            }
            else
            {
                merged.Add(current);
                current = periods[i];
            }
        }
        merged.Add(current);
        return merged;
    }

    /// <summary>
    /// Find gaps (uncovered periods) between merged insurance periods within the given range.
    /// </summary>
    private static List<(DateTime Start, DateTime End)> FindInsuranceGaps(
        List<(DateTime Start, DateTime End)> mergedPeriods, DateTime rangeStart, DateTime rangeEnd)
    {
        var gaps = new List<(DateTime Start, DateTime End)>();
        var cursor = rangeStart;

        foreach (var period in mergedPeriods)
        {
            // Clamp to our range
            var periodStart = period.Start < rangeStart ? rangeStart : period.Start;
            var periodEnd = period.End > rangeEnd ? rangeEnd : period.End;

            if (periodStart > rangeEnd) break;

            if (cursor < periodStart)
            {
                // There's a gap before this policy period
                gaps.Add((cursor, periodStart));
            }

            cursor = periodEnd > cursor ? periodEnd : cursor;
        }

        // Gap after the last policy period until range end
        if (cursor < rangeEnd)
        {
            gaps.Add((cursor, rangeEnd));
        }

        return gaps;
    }
}
