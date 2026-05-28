namespace Shared.Messaging.Contracts;

public record BookingCreatedMessage(
    Guid BookingId,
    Guid AircraftId,
    Guid PilotId,
    Guid CompanyId,
    DateTime StartDateTime,
    DateTime EndDateTime,
    DateTime CreatedAt);
