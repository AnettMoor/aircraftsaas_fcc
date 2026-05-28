namespace Shared.Contracts.Booking.DTOs;

public record BookingBasicDto(
    Guid Id,
    Guid AircraftId,
    Guid PilotId,
    DateTime StartTime,
    DateTime EndTime,
    string Status);
