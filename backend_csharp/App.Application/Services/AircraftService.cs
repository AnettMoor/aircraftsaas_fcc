using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain.Entities;
using Base.Domain;

namespace App.Application.Services;

public class AircraftService : IAircraftService
{
    private readonly IAppUOW _uow;
    
    public AircraftService(IAppUOW uow)
    {
        _uow = uow;
    }
    
    public async Task<AircraftDto?> GetByIdAsync(Guid id, Guid? companyId = null)
    {
        var aircraft = await _uow.AircraftRepository.GetByIdWithIncludesAsync(id, companyId);
        
        if (aircraft == null)
            return null;
        
        return MapToDto(aircraft);
    }
    
    public async Task<IEnumerable<AircraftDto>> GetAllAsync(Guid companyId)
    {
        var aircraft = await _uow.AircraftRepository.GetAllForCompanyAsync(companyId);
        return aircraft.Select(MapToDto);
    }
    
    public async Task<IEnumerable<AircraftDto>> SearchAsync(AircraftSearchDto search)
    {
        var aircraft = await _uow.AircraftRepository.SearchAsync(
            make: search.Make,
            model: search.Model,
            category: search.Category,
            location: search.Location,
            maxHourlyRate: search.MaxHourlyRate,
            year: search.Year,
            available: true,
            page: search.Page,
            pageSize: search.PageSize);

        var aircraftList = aircraft.ToList();

        // When date-range filters are active, exclude aircraft that cannot
        // actually be booked: no valid insurance, overlapping maintenance,
        // or conflicting bookings.
        if (search.StartDate.HasValue && search.EndDate.HasValue)
        {
            var start = DateTime.SpecifyKind(search.StartDate.Value, DateTimeKind.Utc);
            var end = DateTime.SpecifyKind(search.EndDate.Value, DateTimeKind.Utc);

            aircraftList = aircraftList.Where(a =>
            {
                // 1. Must have at least one active insurance policy
                var policies = a.InsurancePolicies?.ToList() ?? new List<InsurancePolicy>();
                if (!policies.Any(p => p.IsActive))
                    return false;

                // 2. No overlapping active maintenance
                var maintenanceRecords = a.MaintenanceRecords?.ToList() ?? new List<MaintenanceRecord>();
                var hasOverlappingMaintenance = maintenanceRecords.Any(m =>
                    !m.IsCompleted && !m.IsDeleted &&
                    (m.Status.ToString() == "Scheduled" || m.Status.ToString() == "InProgress") &&
                    m.StartDate.HasValue && m.EndDate.HasValue &&
                    start < m.EndDate.Value && end > m.StartDate.Value);
                if (hasOverlappingMaintenance)
                    return false;

                // 3. No conflicting bookings
                var bookings = a.Bookings ?? new List<Booking>();
                var hasConflictingBooking = bookings.Any(b =>
                    b.Status != Domain.Enums.EBookingStatus.Cancelled &&
                    b.Status != Domain.Enums.EBookingStatus.Rejected &&
                    ((start >= b.StartDateTime && start < b.EndDateTime) ||
                     (end > b.StartDateTime && end <= b.EndDateTime) ||
                     (start <= b.StartDateTime && end >= b.EndDateTime)));
                if (hasConflictingBooking)
                    return false;

                return true;
            }).ToList();
        }
        
        var dtos = aircraftList.Select(MapToDto).ToList();

        // Filter by computed status (InsuranceInactive, Maintenance, Available, etc.)
        if (!string.IsNullOrWhiteSpace(search.Status))
        {
            dtos = dtos.Where(d => string.Equals(d.Status, search.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return dtos;
    }
    
    public async Task<IEnumerable<AircraftDto>> GetAllDeletedAsync(Guid companyId)
    {
        var aircraft = await _uow.AircraftRepository.GetAllDeletedForCompanyAsync(companyId);
        return aircraft.Select(MapToDto);
    }
    
    public async Task<AircraftDto> CreateAsync(CreateAircraftDto dto, Guid companyId, string createdBy)
    {
        var aircraft = new Aircraft
        {
            RegistrationNumber = dto.RegistrationNumber,
            Make = dto.Make,
            Model = dto.Model,
            Year = dto.Year,
            Category = new LangStr(dto.Category),
            RequiredLicenseType = dto.RequiredLicenseType,
            TotalAirspeedHours = dto.TotalAirspeedHours,
            HourlyRate = dto.HourlyRate,
            BaseAirportId = dto.BaseAirportId,
            Description = new LangStr(dto.Description ?? ""),
            IsAvailable = true,
            CompanyId = companyId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        
        _uow.AircraftRepository.Add(aircraft);
        await _uow.SaveChangesAsync();

        // Handle inline insurance policy
        if (dto.InsurancePolicy != null)
        {
            var insurancePolicy = new InsurancePolicy
            {
                AircraftId = aircraft.Id,
                PolicyNumber = dto.InsurancePolicy.PolicyNumber,
                InsuranceProvider = new LangStr(dto.InsurancePolicy.InsuranceProvider),
                StartDate = DateTime.SpecifyKind(dto.InsurancePolicy.StartDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(dto.InsurancePolicy.EndDate, DateTimeKind.Utc),
                CoverageAmount = dto.InsurancePolicy.CoverageAmount,
                CoverageType = new LangStr(dto.InsurancePolicy.CoverageType)
            };

            _uow.AircraftRepository.AddInsurancePolicy(insurancePolicy);
            await _uow.SaveChangesAsync();
        }

        // Reload with includes
        var created = await _uow.AircraftRepository.GetByIdWithIncludesAsync(aircraft.Id, companyId);
        return MapToDto(created!);
    }
    
    public async Task<AircraftDto> UpdateAsync(Guid id, UpdateAircraftDto dto, Guid companyId, string updatedBy)
    {
        var aircraft = await _uow.AircraftRepository.GetByIdForCompanyTrackingAsync(id, companyId);
        
        if (aircraft == null)
        {
            throw new InvalidOperationException("Aircraft not found");
        }
        
        aircraft.RegistrationNumber = dto.RegistrationNumber;
        aircraft.Make = dto.Make;
        aircraft.Model = dto.Model;
        aircraft.Year = dto.Year;
        aircraft.Category.SetTranslation(dto.Category);
        aircraft.RequiredLicenseType = dto.RequiredLicenseType;
        aircraft.TotalAirspeedHours = dto.TotalAirspeedHours;
        aircraft.HourlyRate = dto.HourlyRate;
        aircraft.BaseAirportId = dto.BaseAirportId;
        aircraft.Description.SetTranslation(dto.Description ?? "");
        aircraft.IsAvailable = dto.IsAvailable;
        aircraft.UpdatedAt = DateTime.UtcNow;
        aircraft.UpdatedBy = updatedBy;
        
        await _uow.SaveChangesAsync();

        // Handle inline insurance policy
        if (dto.InsurancePolicy != null)
        {
            var insurancePolicy = new InsurancePolicy
            {
                AircraftId = id,
                PolicyNumber = dto.InsurancePolicy.PolicyNumber,
                InsuranceProvider = new LangStr(dto.InsurancePolicy.InsuranceProvider),
                StartDate = DateTime.SpecifyKind(dto.InsurancePolicy.StartDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(dto.InsurancePolicy.EndDate, DateTimeKind.Utc),
                CoverageAmount = dto.InsurancePolicy.CoverageAmount,
                CoverageType = new LangStr(dto.InsurancePolicy.CoverageType)
            };

            _uow.AircraftRepository.AddInsurancePolicy(insurancePolicy);
            await _uow.SaveChangesAsync();
        }

        // Reload with includes (IDOR: scope to the same company)
        var updated = await _uow.AircraftRepository.GetByIdWithIncludesAsync(id, companyId);
        
        return MapToDto(updated!);
    }
    
    public async Task DeleteAsync(Guid id, Guid companyId, string deletedBy)
    {
        var aircraft = await _uow.AircraftRepository.GetByIdIgnoreFiltersTrackingAsync(id, companyId);
        
        if (aircraft == null)
        {
            throw new InvalidOperationException("Aircraft not found");
        }
        
        aircraft.SoftDelete(deletedBy);
        
        await _uow.SaveChangesAsync();
    }
    
    public async Task RestoreAsync(Guid id, Guid companyId, string restoredBy)
    {
        var aircraft = await _uow.AircraftRepository.GetDeletedByIdTrackingAsync(id, companyId);
        
        if (aircraft == null)
        {
            throw new InvalidOperationException("Aircraft not found or not deleted");
        }
        
        // Restore
        aircraft.Restore();
        aircraft.UpdatedAt = DateTime.UtcNow;
        aircraft.UpdatedBy = restoredBy;
        
        await _uow.SaveChangesAsync();
    }
    
    public async Task<IEnumerable<AircraftDto>> GetAvailableAsync(DateTime start, DateTime end, string? location = null)
    {
        var available = await _uow.AircraftRepository.GetAvailableAsync(start, end, location);
        return available.Select(MapToDto);
    }
    
    public async Task<IEnumerable<AircraftPhotoDto>> GetPhotosAsync(Guid aircraftId, Guid? companyId = null)
    {
        if (companyId.HasValue)
        {
            // Verify the aircraft belongs to the company
            var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId.Value);
            if (!aircraftExists)
                throw new InvalidOperationException("Aircraft not found for the given company.");
        }

        var photos = await _uow.AircraftRepository.GetPhotosAsync(aircraftId);
        return photos.Select(MapPhotoToDto);
    }

    public async Task<AircraftPhotoDto> AddPhotoAsync(Guid aircraftId, Guid companyId, AddAircraftPhotoDto dto, string addedBy)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        // If this photo is marked primary, clear existing primary
        if (dto.IsPrimary)
        {
            var existingPrimary = await _uow.AircraftRepository.GetPrimaryPhotosTrackingAsync(aircraftId);
            foreach (var ep in existingPrimary)
                ep.IsPrimary = false;
        }

        // Auto-assign display order if not provided
        var displayOrder = dto.DisplayOrder;
        if (displayOrder == 0)
        {
            var maxOrder = await _uow.AircraftRepository.GetMaxPhotoDisplayOrderAsync(aircraftId) ?? 0;
            displayOrder = maxOrder + 1;
        }

        var photo = new AircraftPhoto
        {
            AircraftId = aircraftId,
            ImageUrl = dto.ImageUrl,
            Description = dto.Description,
            IsPrimary = dto.IsPrimary,
            DisplayOrder = displayOrder,
            UploadedAt = DateTime.UtcNow,
        };

        _uow.AircraftRepository.AddPhoto(photo);
        await _uow.SaveChangesAsync();

        return MapPhotoToDto(photo);
    }

    public async Task SetPrimaryPhotoAsync(Guid photoId, Guid aircraftId, Guid companyId)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var photo = await _uow.AircraftRepository.GetPhotoByIdTrackingAsync(photoId, aircraftId);
        if (photo == null)
            throw new InvalidOperationException("Photo not found.");

        // Clear existing primary on all other photos for this aircraft
        var otherPrimaries = (await _uow.AircraftRepository.GetPrimaryPhotosTrackingAsync(aircraftId))
            .Where(p => p.Id != photoId);
        foreach (var p in otherPrimaries)
            p.IsPrimary = false;

        photo.IsPrimary = true;

        await _uow.SaveChangesAsync();
    }

    public async Task DeletePhotoAsync(Guid photoId, Guid aircraftId, Guid companyId, string deletedBy)
    {
        // Verify aircraft belongs to company
        var aircraftExists = await _uow.AircraftRepository.ExistsForCompanyAsync(aircraftId, companyId);
        if (!aircraftExists)
            throw new InvalidOperationException("Aircraft not found for the given company.");

        var photo = await _uow.AircraftRepository.GetPhotoByIdTrackingAsync(photoId, aircraftId);
        if (photo == null)
            throw new InvalidOperationException("Photo not found.");

        photo.SoftDelete(deletedBy);

        await _uow.SaveChangesAsync();
    }

    private static AircraftPhotoDto MapPhotoToDto(AircraftPhoto photo) => new()
    {
        Id = photo.Id,
        AircraftId = photo.AircraftId,
        ImageUrl = photo.ImageUrl,
        Description = photo.Description,
        IsPrimary = photo.IsPrimary,
        DisplayOrder = photo.DisplayOrder,
        UploadedAt = photo.UploadedAt,
    };

    private static AircraftDto MapToDto(Aircraft aircraft)
    {
        var photoUrls = aircraft.Photos?.Select(p => p.Url).ToList() ?? new List<string>();
        var reviews = aircraft.Reviews?.ToList() ?? new List<Review>();
        var avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

        var policies = aircraft.InsurancePolicies?.ToList() ?? new List<InsurancePolicy>();
        var activePolicies = policies.Where(p => p.IsActive).ToList();
        var isInsured = activePolicies.Any();
        var insuranceExpiryDate = activePolicies.Any()
            ? activePolicies.Min(p => p.EndDate)
            : (DateTime?)null;

        // Check for active (in-progress/scheduled) maintenance that overlaps with today
        var maintenanceRecords = aircraft.MaintenanceRecords?.ToList() ?? new List<MaintenanceRecord>();
        var now = DateTime.UtcNow;
        var hasActiveMaintenance = maintenanceRecords.Any(m =>
            !m.IsCompleted && !m.IsDeleted &&
            (m.Status.ToString() == "Scheduled" || m.Status.ToString() == "InProgress") &&
            (m.StartDate ?? m.MaintenanceDate) <= now &&
            (m.EndDate ?? m.StartDate ?? m.MaintenanceDate) >= now);

        // Compute effective status
        string status;
        if (hasActiveMaintenance)
            status = "Maintenance";
        else if (!isInsured)
            status = "InsuranceInactive";
        else if (!aircraft.IsAvailable)
            status = "Unavailable";
        else
            status = "Available";

        return new AircraftDto
        {
            Id = aircraft.Id,
            RegistrationNumber = aircraft.RegistrationNumber,
            Make = aircraft.Make,
            Model = aircraft.Model,
            Year = aircraft.Year,
            Category = aircraft.Category.ToString(),
            RequiredLicenseType = aircraft.RequiredLicenseType,
            TotalAirspeedHours = aircraft.TotalAirspeedHours,
            HourlyRate = aircraft.HourlyRate,
            BaseAirportId = aircraft.BaseAirportId,
            BaseAirportName = aircraft.BaseAirport?.Name.ToString() ?? "",
            Description = aircraft.Description?.ToString() ?? "",
            IsAvailable = aircraft.IsAvailable,
            CompanyId = aircraft.CompanyId,
            CompanyName = aircraft.Company?.CompanyName.ToString() ?? "",
            CompanyEmail = aircraft.Company?.Email,
            CompanyPhone = aircraft.Company?.Phone,
            PhotoUrls = photoUrls,
            AverageRating = avgRating,
            ReviewCount = reviews.Count,
            IsInsured = isInsured,
            InsuranceExpiryDate = insuranceExpiryDate,
            HasActiveMaintenance = hasActiveMaintenance,
            Status = status,
            InsurancePolicies = policies.Select(p => new InsurancePolicyDto
            {
                Id = p.Id,
                AircraftId = p.AircraftId,
                PolicyNumber = p.PolicyNumber,
                InsuranceProvider = p.InsuranceProvider.ToString(),
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CoverageAmount = p.CoverageAmount,
                CoverageType = p.CoverageType.ToString(),
                IsActive = p.IsActive
            }).ToList()
        };
    }
}
