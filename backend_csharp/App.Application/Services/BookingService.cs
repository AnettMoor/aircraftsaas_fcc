using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain.Entities;
using App.Domain.Enums;

namespace App.Application.Services;

public class BookingService : IBookingService
{
    private readonly IAppUOW _uow;
    
    public BookingService(IAppUOW uow)
    {
        _uow = uow;
    }
    
    public async Task<BookingDto?> GetByIdAsync(Guid id, Guid? companyId = null, Guid? userId = null)
    {
        var booking = await _uow.BookingRepository.GetByIdWithIncludesAsync(id, companyId, userId);
        
        if (booking == null)
            return null;
        
        return MapToDto(booking);
    }
    
    public async Task<IEnumerable<BookingDto>> GetAllForPilotAsync(Guid userId)
    {
        var bookings = await _uow.BookingRepository.GetAllForPilotAsync(userId);
        return bookings.Select(MapToDto);
    }
    
    public async Task<IEnumerable<BookingDto>> GetAllForCompanyAsync(Guid companyId)
    {
        var bookings = await _uow.BookingRepository.GetAllForCompanyAsync(companyId);
        return bookings.Select(MapToDto);
    }
    
    public async Task<BookingDto> RequestBookingAsync(CreateBookingDto dto, Guid userId)
    {
        // Validate that the user (pilot) actually exists in the database.
        var userExists = await _uow.BookingRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new InvalidOperationException(
                "Your user account was not found. Please log out and log back in.");
        }

        // Load aircraft early so we can reuse it for validation (avoids duplicate FindAsync)
        var aircraft = await _uow.AircraftRepository.GetByIdWithIncludesAsync(dto.AircraftId);
        
        if (aircraft == null)
        {
            throw new InvalidOperationException("Aircraft not found");
        }
        
        if (!aircraft.IsAvailable)
        {
            throw new InvalidOperationException("Aircraft is not available for booking");
        }

        // Validate booking
        if (!await ValidateBookingAsync(dto.AircraftId, dto.StartDateTime, dto.EndDateTime))
        {
            throw new InvalidOperationException("Aircraft is not available for the selected dates");
        }
        
        // Validate pilot license for the aircraft (use RequiredLicenseType set by company owner)
        var requiredLicenseType = aircraft.RequiredLicenseType;
        var bookingDate = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc);
        if (!await _uow.BookingRepository.HasValidLicenseAsync(userId, requiredLicenseType, bookingDate))
        {
            var hasAnyLicense = await _uow.BookingRepository.HasAnyLicenseAsync(userId);
            throw new InvalidOperationException(
                hasAnyLicense
                    ? "Your license for this aircraft type is expired or invalid. Please update your license before booking."
                    : "You must add a valid pilot license before you can book an aircraft.");
        }
        
        // Validate aircraft insurance coverage (always block when invalid — no insurance = no booking)
        if (!await ValidateInsuranceCoverageAsync(dto.AircraftId, dto.StartDateTime, dto.EndDateTime))
        {
            throw new InvalidOperationException(
                "Aircraft does not have valid insurance coverage for the booking period. Booking is not allowed.");
        }
        
        // Calculate total amount
        var hours = (decimal)(dto.EndDateTime - dto.StartDateTime).TotalHours;
        var totalAmount = hours * aircraft.HourlyRate;
        
        // Create booking
        var booking = new Booking
        {
            AircraftId = dto.AircraftId,
            PilotId = userId,
            CompanyId = aircraft.CompanyId,
            StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc),
            EndDateTime = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc),
            Purpose = dto.Purpose ?? string.Empty,
            Status = EBookingStatus.Requested,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };
        
        _uow.BookingRepository.Add(booking);
        await _uow.SaveChangesAsync();
        
        // Create an AircraftAvailability record to block the booked dates on the calendar
        var availabilityBlock = new AircraftAvailability
        {
            AircraftId = dto.AircraftId,
            BookingId = booking.Id,
            StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc),
            EndDateTime = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc),
            AvailabilityType = "Booked",
            Reason = $"Booked by pilot (Booking #{booking.Id.ToString()[..8]})"
        };
        
        _uow.AircraftAvailabilityRepository.Add(availabilityBlock);
        await _uow.SaveChangesAsync();
        
        // Reload with includes (IDOR: scope to the pilot and company)
        var created = await _uow.BookingRepository.GetByIdWithIncludesAsync(booking.Id, companyId: booking.CompanyId, userId: userId);
        
        return MapToDto(created!);
    }
    
    public async Task<BookingDto> UpdateBookingAsync(UpdateBookingDto dto, Guid userId)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingWithIncludesAsync(dto.Id);
        
        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }
        
        // Only the pilot who created the booking can edit it
        if (booking.PilotId != userId)
        {
            throw new UnauthorizedAccessException("You can only edit your own bookings");
        }
        
        // Only Pending or Requested bookings can be edited
        if (booking.Status != EBookingStatus.Pending && booking.Status != EBookingStatus.Requested)
        {
            throw new InvalidOperationException("Only pending or requested bookings can be edited");
        }
        
        // Validate new time slot (exclude the current booking from overlap check)
        var start = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc);
        
        var hasOverlap = await _uow.BookingRepository.HasOverlappingBookingsAsync(
            booking.AircraftId, start, end, dto.Id);
        
        if (hasOverlap)
        {
            throw new InvalidOperationException("Aircraft is not available for the selected dates");
        }
        
        // Recalculate total amount (IDOR: scope to the booking's company)
        var aircraft = await _uow.AircraftRepository.FindAsync(booking.AircraftId, companyId: booking.CompanyId);
        if (aircraft == null)
        {
            throw new InvalidOperationException("Aircraft not found");
        }
        
        var hours = (decimal)(end - start).TotalHours;
        var totalAmount = hours * aircraft.HourlyRate;
        
        // Update booking fields
        booking.StartDateTime = start;
        booking.EndDateTime = end;
        booking.Purpose = dto.Purpose ?? string.Empty;
        booking.TotalAmount = totalAmount;
        booking.UpdatedAt = DateTime.UtcNow;
        
        // Update the corresponding availability block dates
        var availabilityBlock = await _uow.AircraftAvailabilityRepository
            .GetByBookingIdTrackingAsync(dto.Id);
        if (availabilityBlock != null)
        {
            availabilityBlock.StartDateTime = start;
            availabilityBlock.EndDateTime = end;
        }
        
        await _uow.SaveChangesAsync();
        
        return MapToDto(booking);
    }
    
    public async Task<BookingDto> ApproveAsync(Guid bookingId, Guid companyId)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingWithIncludesAsync(bookingId, companyId);
        
        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }
        
        if (!booking.CanApprove())
        {
            throw new InvalidOperationException("Only requested bookings can be approved");
        }
        
        booking.Status = EBookingStatus.Approved;
        booking.ApprovedAt = DateTime.UtcNow;
        
        await _uow.SaveChangesAsync();
        
        return MapToDto(booking);
    }
    
    public async Task<BookingDto> RejectAsync(Guid bookingId, Guid companyId, string reason)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingWithIncludesAsync(bookingId, companyId);
        
        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }
        
        if (!booking.CanReject())
        {
            throw new InvalidOperationException("Only requested bookings can be rejected");
        }
        
        booking.Status = EBookingStatus.Rejected;
        booking.RejectionReason = reason;
        booking.UpdatedAt = DateTime.UtcNow;
        
        // Remove the availability block for the rejected booking
        var availabilityBlock = await _uow.AircraftAvailabilityRepository
            .GetByBookingIdTrackingAsync(bookingId);
        if (availabilityBlock != null)
        {
            availabilityBlock.SoftDelete("system");
        }
        
        await _uow.SaveChangesAsync();
        
        return MapToDto(booking);
    }
    
    public async Task<BookingDto> ConfirmPaymentAsync(Guid bookingId, PaymentDto payment, Guid userId)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingWithPaymentsAsync(bookingId);
        
        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }
        
        // IDOR protection: only the pilot who made the booking can pay for it
        if (booking.PilotId != userId)
        {
            throw new UnauthorizedAccessException("You can only pay for your own bookings");
        }
        
        if (!booking.CanPay())
        {
            throw new InvalidOperationException("Only approved bookings can be paid");
        }
        
        // Create payment record
        var paymentRecord = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            PaymentMethod = payment.PaymentMethod,
            TransactionId = payment.TransactionId,
            Status = EPaymentStatus.Completed.ToString(),
            PaidAt = DateTime.UtcNow
        };
        
        _uow.BookingRepository.AddPayment(paymentRecord);
        
        booking.Status = EBookingStatus.Paid;
        booking.PaidAt = DateTime.UtcNow;
        
        await _uow.SaveChangesAsync();
        
        return MapToDto(booking);
    }
    
    public async Task<BookingDto> CancelAsync(Guid bookingId, Guid userId)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingAsync(bookingId);
        
        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }
        
        // Check if user is the pilot or company owner
        if (booking.PilotId != userId)
        {
            var isCompanyOwner = await _uow.BookingRepository.IsCompanyOwnerAsync(userId, booking.CompanyId);
            
            if (!isCompanyOwner)
            {
                throw new UnauthorizedAccessException("You can only cancel your own bookings");
            }
        }
        
        if (!booking.CanCancel())
        {
            throw new InvalidOperationException("Completed bookings cannot be cancelled");
        }
        
        booking.Status = EBookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;
        
        // Remove the availability block for the cancelled booking
        var availabilityBlock = await _uow.AircraftAvailabilityRepository
            .GetByBookingIdTrackingAsync(bookingId);
        if (availabilityBlock != null)
        {
            availabilityBlock.SoftDelete(userId.ToString());
        }
        
        await _uow.SaveChangesAsync();
        
        return MapToDto(booking);
    }
    
    public async Task<BookingDto> CompleteAsync(Guid bookingId, Guid companyId)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingWithIncludesAsync(bookingId, companyId);
        
        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }
        
        if (!booking.CanComplete())
        {
            throw new InvalidOperationException("Only paid bookings can be completed");
        }
        
        booking.Status = EBookingStatus.Completed;
        booking.CompletedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;
        
        // Update aircraft hours
        var aircraft = await _uow.AircraftRepository.GetByIdForCompanyTrackingAsync(booking.AircraftId, companyId);
        if (aircraft != null)
        {
            var hours = (decimal)(booking.EndDateTime - booking.StartDateTime).TotalHours;
            aircraft.TotalAirspeedHours += (int)hours;
        }
        
        // Remove the availability block for the completed booking (flight is done)
        var availabilityBlock = await _uow.AircraftAvailabilityRepository
            .GetByBookingIdTrackingAsync(bookingId);
        if (availabilityBlock != null)
        {
            availabilityBlock.SoftDelete("system");
        }
        
        await _uow.SaveChangesAsync();
        
        return MapToDto(booking);
    }
    
    public async Task<bool> ValidateBookingAsync(Guid aircraftId, DateTime start, DateTime end)
    {
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        // Check for overlapping bookings
        var hasOverlap = await _uow.BookingRepository.HasOverlappingBookingsAsync(aircraftId, start, end);
        
        if (hasOverlap)
            return false;
        
        // Check for maintenance blocks
        var maintenanceRecords = await _uow.MaintenanceRecordRepository
            .GetScheduledForAircraftInRangeAsync(aircraftId, start, end);
        
        if (maintenanceRecords.Any())
            return false;
        
        // Check for blocking availability records (Blocked / Maintenance type entries)
        var hasBlockingAvailability = await _uow.AircraftAvailabilityRepository
            .HasBlockingAvailabilityAsync(aircraftId, start, end);
        if (hasBlockingAvailability)
            return false;
        
        return true;
    }
    
    public async Task<bool> ValidatePilotLicenseAsync(Guid userId, Guid aircraftId, DateTime bookingDate)
    {
        bookingDate = DateTime.SpecifyKind(bookingDate, DateTimeKind.Utc);

        // Get the aircraft to determine required license type
        var aircraft = await _uow.AircraftRepository.FindAsync(aircraftId);
        if (aircraft == null)
            return false;
        
        // Use RequiredLicenseType set by company owner
        var requiredLicenseType = aircraft.RequiredLicenseType;
        
        // Check if user has a valid, non-expired license for this aircraft type
        return await _uow.BookingRepository.HasValidLicenseAsync(userId, requiredLicenseType, bookingDate);
    }
    
    public async Task<bool> ValidateInsuranceCoverageAsync(Guid aircraftId, DateTime start, DateTime end)
    {
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        return await _uow.BookingRepository.HasInsuranceCoverageAsync(aircraftId, start, end);
    }
    
    private static string MapAircraftCategoryToLicenseType(string aircraftCategory)
    {
        return aircraftCategory switch
        {
            "SingleEngineLand" => "PPL",
            "MultiEngineLand" => "CPL",
            "SingleEngineSea" => "PPL",
            "MultiEngineSea" => "CPL",
            "Helicopter" => "CPL",
            "Gyroplane" => "PPL",
            _ => "PPL"
        };
    }
    
    private static BookingDto MapToDto(Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            AircraftId = booking.AircraftId,
            AircraftName = booking.Aircraft != null 
                ? $"{booking.Aircraft.Make} {booking.Aircraft.Model} ({booking.Aircraft.RegistrationNumber})"
                : "",
            PilotId = booking.PilotId,
            PilotName = booking.Pilot != null
                ? $"{booking.Pilot.FirstName} {booking.Pilot.LastName}"
                : "",
            StartDateTime = booking.StartDateTime,
            EndDateTime = booking.EndDateTime,
            Status = booking.Status,
            Purpose = booking.Purpose?.Translate(),
            TotalAmount = booking.TotalAmount,
            RejectionReason = booking.RejectionReason?.Translate(),
            ApprovedAt = booking.ApprovedAt,
            PaidAt = booking.PaidAt,
            CompletedAt = booking.CompletedAt,
            CancelledAt = booking.CancelledAt,
            CompanyId = booking.CompanyId,
            CreatedAt = booking.CreatedAt
        };
    }
}
