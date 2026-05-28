using Fleet.Application.Contracts;
using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace Fleet.WebHost.Consumers;

public class BookingCancelledConsumer : RabbitMqConsumerBase<BookingCancelledMessage>
{
    public BookingCancelledConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingCancelledConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "booking.events",
            queue: "fleet.booking.cancelled",
            routingKey: "booking.cancelled")
    { }

    protected override async Task HandleMessageAsync(
        BookingCancelledMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var uow = sp.GetRequiredService<IFleetUOW>();
        var logger = sp.GetRequiredService<ILogger<BookingCancelledConsumer>>();

        var availability = await uow.AircraftAvailabilityRepository
            .GetByBookingIdTrackingAsync(message.BookingId);

        if (availability == null)
        {
            logger.LogWarning("No availability block found for BookingId={BookingId}.",
                message.BookingId);
            return;
        }

        availability.SoftDelete("system");
        await uow.SaveChangesAsync();

        logger.LogInformation(
            "Released availability block for aircraft {AircraftId} (cancelled Booking: {BookingId})",
            message.AircraftId, message.BookingId);
    }
}
