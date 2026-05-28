using Booking.Application.Interfaces;
using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace Booking.WebHost.Publishers;

public class BookingEventPublisher : IBookingEventPublisher
{
    private readonly RabbitMqPublisher _publisher;
    private const string Exchange = "booking.events";

    public BookingEventPublisher(RabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task PublishBookingCreatedAsync(
        Guid bookingId, Guid aircraftId, Guid pilotId,
        Guid companyId, DateTime startDateTime, DateTime endDateTime)
    {
        return _publisher.PublishAsync(Exchange, "booking.created",
            new BookingCreatedMessage(bookingId, aircraftId, pilotId,
                companyId, startDateTime, endDateTime, DateTime.UtcNow));
    }

    public Task PublishBookingCancelledAsync(
        Guid bookingId, Guid aircraftId, Guid pilotId, Guid companyId)
    {
        return _publisher.PublishAsync(Exchange, "booking.cancelled",
            new BookingCancelledMessage(bookingId, aircraftId, pilotId,
                companyId, DateTime.UtcNow));
    }

    public Task PublishBookingCompletedAsync(
        Guid bookingId, Guid aircraftId, Guid pilotId, Guid companyId)
    {
        return _publisher.PublishAsync(Exchange, "booking.completed",
            new BookingCompletedMessage(bookingId, aircraftId, pilotId,
                companyId, DateTime.UtcNow));
    }
}
