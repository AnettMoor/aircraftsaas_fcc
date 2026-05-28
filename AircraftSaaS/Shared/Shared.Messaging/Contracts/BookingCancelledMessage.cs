namespace Shared.Messaging.Contracts;

public record BookingCancelledMessage(
    Guid BookingId,
    Guid AircraftId,
    Guid PilotId,
    Guid CompanyId,
    DateTime CancelledAt);
