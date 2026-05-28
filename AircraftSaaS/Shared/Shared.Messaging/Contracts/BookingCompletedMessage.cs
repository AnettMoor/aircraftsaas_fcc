namespace Shared.Messaging.Contracts;

public record BookingCompletedMessage(
    Guid BookingId,
    Guid AircraftId,
    Guid PilotId,
    Guid CompanyId,
    DateTime CompletedAt);
