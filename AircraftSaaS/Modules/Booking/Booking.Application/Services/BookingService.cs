using Booking.Application.Contracts;
using Booking.Application.DTOs;
using Booking.Application.Interfaces;
using Booking.Domain.Entities;
using Booking.Domain.Enums;
using Shared.Contracts.Fleet;
using Shared.Contracts.Users;

namespace Booking.Application.Services;

internal sealed class BookingService : IBookingService
{
    private readonly IBookingUOW _uow;
    private readonly IBookingEventPublisher _eventPublisher;
    private readonly IFleetModuleApi _fleetApi;
    private readonly IUsersModuleApi _usersApi;

    public BookingService(IBookingUOW uow, IBookingEventPublisher eventPublisher, IFleetModuleApi fleetApi, IUsersModuleApi usersApi)
    {
        _uow = uow;
        _eventPublisher = eventPublisher;
        _fleetApi = fleetApi;
        _usersApi = usersApi;
    }

    public async Task<BookingDto?> GetByIdAsync(Guid id, Guid? companyId = null, Guid? userId = null)
    {
        var booking = await _uow.BookingRepository.GetByIdWithIncludesAsync(id, companyId, userId);

        if (booking == null)
            return null;

        return await MapToDtoAsync(booking);
    }

    public async Task<IEnumerable<BookingDto>> GetAllForPilotAsync(Guid userId)
    {
        var bookings = await _uow.BookingRepository.GetAllForPilotAsync(userId);
        return await MapToDtosAsync(bookings);
    }

    public async Task<IEnumerable<BookingDto>> GetAllForCompanyAsync(Guid companyId)
    {
        var bookings = await _uow.BookingRepository.GetAllForCompanyAsync(companyId);
        return await MapToDtosAsync(bookings);
    }

    public async Task<BookingDto> RequestBookingAsync(CreateBookingDto dto, Guid userId)
    {
        // ASP.NET JSON binding produces DateTime with Kind=Unspecified, which Npgsql
        // refuses to write into PostgreSQL "timestamp with time zone" columns
        // (e.g. inside HasOverlappingBookingsAsync below). Normalise once here so
        // every downstream EF query, Fleet HTTP call, and entity write uses UTC.
        dto.StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc);
        dto.EndDateTime   = DateTime.SpecifyKind(dto.EndDateTime,   DateTimeKind.Utc);

        // Validate that the user (pilot) actually exists via Users module
        var user = await _usersApi.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException(
                "Your user account was not found. Please log out and log back in.");
        }

        // Load aircraft info from Fleet module, request/response
        var aircraft = await _fleetApi.GetAircraftByIdAsync(dto.AircraftId);

        if (aircraft == null)
        {
            throw new InvalidOperationException("Aircraft not found");
        }

        // 1) Check Fleet-side blocking rows (Blocked / Maintenance / "Booked" from prior bookings).
        //    Returns FALSE both when a real blocking row exists AND when the Fleet service is
        //    unreachable (the proxy swallows HttpRequestException). Two distinct error messages
        //    so the UI can tell us which.
        bool isFleetAvailable;
        try
        {
            isFleetAvailable = await _fleetApi.CheckAircraftAvailabilityAsync(
                dto.AircraftId, dto.StartDateTime, dto.EndDateTime);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the Fleet service to verify availability: {ex.Message}");
        }

        if (!isFleetAvailable)
        {
            throw new InvalidOperationException(
                "Aircraft is blocked by an existing Fleet availability row (Blocked / Maintenance / Booked) overlapping this window. " +
                "If the UI shows the slot as free, a stale 'Booked' row from a previous booking attempt may exist in Fleet.AircraftAvailability.");
        }

        // 2) Check for an existing non-cancelled booking in our own DB.
        var hasOverlap = await _uow.BookingRepository.HasOverlappingBookingsAsync(
            dto.AircraftId, dto.StartDateTime, dto.EndDateTime);
        if (hasOverlap)
        {
            throw new InvalidOperationException(
                "You (or another pilot) already have a non-cancelled booking for this aircraft that overlaps this window.");
        }

        // Validate pilot license via Users module
        var requiredLicenseType = aircraft.RequiredLicenseType ?? "PPL";
        var bookingDate = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc);

        bool hasValidLicense;
        try
        {
            hasValidLicense = await _usersApi.CheckUserLicenseAsync(
                userId, requiredLicenseType, bookingDate);
        }
        catch (Exception ex)
        {
            // A 5xx / transport failure from the Users service must NOT masquerade
            // as "missing license". Surface the real cause to the caller.
            throw new InvalidOperationException(
                $"Could not verify your pilot license with the Users service: {ex.Message}");
        }

        if (!hasValidLicense)
        {
            throw new InvalidOperationException(
                $"You must have a valid '{requiredLicenseType}' pilot license that covers the booking date " +
                $"({bookingDate:yyyy-MM-dd}) before you can book this aircraft.");
        }

        // Validate aircraft insurance coverage via Fleet module
        if (!await ValidateInsuranceCoverageAsync(dto.AircraftId, dto.StartDateTime, dto.EndDateTime))
        {
            throw new InvalidOperationException(
                "Aircraft does not have valid insurance coverage for the booking period. Booking is not allowed.");
        }

        // Create booking
        // Note: HourlyRate is not available via AircraftBasicDto.
        // TotalAmount should be calculated by a Fleet pricing query or provided by the caller.
        // For now, set to 0 — the API layer or a future pricing query can populate this.
        var booking = new Domain.Entities.Booking
        {
            AircraftId = dto.AircraftId,
            PilotId = userId,
            CompanyId = aircraft.CompanyId,
            StartDateTime = DateTime.SpecifyKind(dto.StartDateTime, DateTimeKind.Utc),
            EndDateTime = DateTime.SpecifyKind(dto.EndDateTime, DateTimeKind.Utc),
            Purpose = dto.Purpose != null ? new Shared.Kernel.Domain.LangStr(dto.Purpose) : null,
            Status = EBookingStatus.Requested,
            TotalAmount = 0m, // TODO: Retrieve hourly rate from Fleet module via pricing query
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };

        _uow.BookingRepository.Add(booking);
        await _uow.SaveChangesAsync();

        // Publish booking created event via RabbitMQ so Fleet service can block aircraft availability
        await _eventPublisher.PublishBookingCreatedAsync(
            booking.Id,
            booking.AircraftId,
            booking.PilotId,
            booking.CompanyId,
            booking.StartDateTime,
            booking.EndDateTime);

        // Reload with includes
        var created = await _uow.BookingRepository.GetByIdWithIncludesAsync(
            booking.Id, companyId: booking.CompanyId, userId: userId);

        return await MapToDtoAsync(created!);
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

        // Check fleet availability for new dates via Fleet module
        var isAvailable = await _fleetApi.CheckAircraftAvailabilityAsync(
            booking.AircraftId, start, end);

        if (!isAvailable)
        {
            throw new InvalidOperationException("Aircraft is not available for the selected dates");
        }

        // Update booking fields
        booking.StartDateTime = start;
        booking.EndDateTime = end;
        booking.Purpose = dto.Purpose != null ? new Shared.Kernel.Domain.LangStr(dto.Purpose) : null;
        booking.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();

        return await MapToDtoAsync(booking);
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

        return await MapToDtoAsync(booking);
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
        booking.RejectionReason = new Shared.Kernel.Domain.LangStr(reason);
        booking.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();

        // Publish cancellation event via RabbitMQ so Fleet service can release the availability block
        await _eventPublisher.PublishBookingCancelledAsync(
            booking.Id, booking.AircraftId, booking.PilotId, booking.CompanyId);

        return await MapToDtoAsync(booking);
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

        return await MapToDtoAsync(booking);
    }

    public async Task<BookingDto> CancelAsync(Guid bookingId, Guid userId)
    {
        var booking = await _uow.BookingRepository.GetByIdTrackingAsync(bookingId);

        if (booking == null)
        {
            throw new InvalidOperationException("Booking not found");
        }

        // Only the pilot or company owner can cancel
        if (booking.PilotId != userId)
        {
            // Cross-module check: is user a company owner?
            // For now, we check against the booking's CompanyId.
            // This could be enhanced with a Users module query.
            throw new UnauthorizedAccessException("You can only cancel your own bookings");
        }

        if (!booking.CanCancel())
        {
            throw new InvalidOperationException("Completed bookings cannot be cancelled");
        }

        booking.Status = EBookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();

        // Publish cancellation event via RabbitMQ so Fleet service can release the availability block
        await _eventPublisher.PublishBookingCancelledAsync(
            booking.Id, booking.AircraftId, booking.PilotId, booking.CompanyId);

        return await MapToDtoAsync(booking);
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

        await _uow.SaveChangesAsync();

        // Publish completion event via RabbitMQ so Fleet service can release the availability block
        // and update aircraft hours
        await _eventPublisher.PublishBookingCompletedAsync(
            booking.Id, booking.AircraftId, booking.PilotId, booking.CompanyId);

        return await MapToDtoAsync(booking);
    }

    public async Task<bool> ValidateBookingAsync(Guid aircraftId, DateTime start, DateTime end)
    {
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        // Check for overlapping bookings (own module data)
        var hasOverlap = await _uow.BookingRepository.HasOverlappingBookingsAsync(aircraftId, start, end);

        if (hasOverlap)
            return false;

        // Check aircraft availability via Fleet module (maintenance blocks, blocking entries)
        var isAvailable = await _fleetApi.CheckAircraftAvailabilityAsync(
            aircraftId, start, end);

        return isAvailable;
    }

    public async Task<bool> ValidatePilotLicenseAsync(Guid userId, Guid aircraftId, DateTime bookingDate)
    {
        bookingDate = DateTime.SpecifyKind(bookingDate, DateTimeKind.Utc);

        // Get the aircraft to determine required license type via Fleet module
        var aircraft = await _fleetApi.GetAircraftByIdAsync(aircraftId);
        if (aircraft == null)
            return false;

        var requiredLicenseType = aircraft.RequiredLicenseType ?? "PPL";

        // Check if user has a valid license via Users module
        return await _usersApi.CheckUserLicenseAsync(userId, requiredLicenseType, bookingDate);
    }

    public async Task<bool> ValidateInsuranceCoverageAsync(Guid aircraftId, DateTime start, DateTime end)
    {
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        // Check aircraft availability (includes insurance check) via Fleet module
        return await _fleetApi.CheckAircraftAvailabilityAsync(aircraftId, start, end);
    }

    /// <summary>
    /// Maps a Booking entity to BookingDto, enriching with cross-module data via MediatR.
    /// Since the Booking module has NO navigation properties to Aircraft or Pilot,
    /// we fetch display names from the Fleet and Users modules.
    /// </summary>
    private async Task<BookingDto> MapToDtoAsync(Domain.Entities.Booking booking)
    {
        // Fetch aircraft name from Fleet module
        var aircraftName = "";
        var aircraft = await _fleetApi.GetAircraftByIdAsync(booking.AircraftId);
        if (aircraft != null)
        {
            aircraftName = $"{aircraft.Model} ({aircraft.Registration})";
        }

        // Fetch pilot name from Users module
        var pilotName = "";
        var pilot = await _usersApi.GetUserByIdAsync(booking.PilotId);
        if (pilot != null)
        {
            pilotName = $"{pilot.FirstName} {pilot.LastName}";
        }

        return new BookingDto
        {
            Id = booking.Id,
            AircraftId = booking.AircraftId,
            AircraftName = aircraftName,
            PilotId = booking.PilotId,
            PilotName = pilotName,
            StartDateTime = booking.StartDateTime,
            EndDateTime = booking.EndDateTime,
            Status = booking.Status,
            Purpose = booking.Purpose?.ToString(),
            TotalAmount = booking.TotalAmount,
            RejectionReason = booking.RejectionReason?.ToString(),
            ApprovedAt = booking.ApprovedAt,
            PaidAt = booking.PaidAt,
            CompletedAt = booking.CompletedAt,
            CancelledAt = booking.CancelledAt,
            CompanyId = booking.CompanyId,
            CreatedAt = booking.CreatedAt
        };
    }

    /// <summary>
    /// Batch-maps a collection of Booking entities to BookingDtos.
    /// Uses batch cross-module API calls to avoid N+1 problems.
    /// </summary>
    private async Task<List<BookingDto>> MapToDtosAsync(IEnumerable<Domain.Entities.Booking> bookings)
    {
        var bookingList = bookings.ToList();
        if (bookingList.Count == 0)
            return new List<BookingDto>();

        // Collect all unique IDs
        var aircraftIds = bookingList.Select(b => b.AircraftId).Distinct();
        var pilotIds = bookingList.Select(b => b.PilotId).Distinct();

        // Batch-fetch cross-module data (2 calls total instead of 2N)
        var aircraftMap = await _fleetApi.GetAircraftsByIdsAsync(aircraftIds);
        var pilotMap = await _usersApi.GetUsersByIdsAsync(pilotIds);

        return bookingList.Select(booking =>
        {
            var aircraftName = "";
            if (aircraftMap.TryGetValue(booking.AircraftId, out var aircraft))
            {
                aircraftName = $"{aircraft.Model} ({aircraft.Registration})";
            }

            var pilotName = "";
            if (pilotMap.TryGetValue(booking.PilotId, out var pilot))
            {
                pilotName = $"{pilot.FirstName} {pilot.LastName}";
            }

            return new BookingDto
            {
                Id = booking.Id,
                AircraftId = booking.AircraftId,
                AircraftName = aircraftName,
                PilotId = booking.PilotId,
                PilotName = pilotName,
                StartDateTime = booking.StartDateTime,
                EndDateTime = booking.EndDateTime,
                Status = booking.Status,
                Purpose = booking.Purpose?.ToString(),
                TotalAmount = booking.TotalAmount,
                RejectionReason = booking.RejectionReason?.ToString(),
                ApprovedAt = booking.ApprovedAt,
                PaidAt = booking.PaidAt,
                CompletedAt = booking.CompletedAt,
                CancelledAt = booking.CancelledAt,
                CompanyId = booking.CompanyId,
                CreatedAt = booking.CreatedAt
            };
        }).ToList();
    }
}
