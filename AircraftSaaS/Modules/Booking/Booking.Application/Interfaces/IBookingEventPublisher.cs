namespace Booking.Application.Interfaces;

/// <summary>
/// Abstraction for publishing booking lifecycle events.
/// In the monolith, events were published via MediatR.
/// In the microservice, events are published via RabbitMQ.
/// </summary>
public interface IBookingEventPublisher
{
    Task PublishBookingCreatedAsync(
        Guid bookingId, Guid aircraftId, Guid pilotId,
        Guid companyId, DateTime startDateTime, DateTime endDateTime);

    Task PublishBookingCancelledAsync(
        Guid bookingId, Guid aircraftId, Guid pilotId, Guid companyId);

    Task PublishBookingCompletedAsync(
        Guid bookingId, Guid aircraftId, Guid pilotId, Guid companyId);
}
