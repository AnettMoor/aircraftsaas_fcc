using Fleet.Application.Contracts;
using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace Fleet.WebHost.Consumers;

public class BookingCompletedConsumer : RabbitMqConsumerBase<BookingCompletedMessage>
{
    public BookingCompletedConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingCompletedConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "booking.events",
            queue: "fleet.booking.completed",
            routingKey: "booking.completed")
    { }

    protected override async Task HandleMessageAsync(
        BookingCompletedMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var uow = sp.GetRequiredService<IFleetUOW>();
        var logger = sp.GetRequiredService<ILogger<BookingCompletedConsumer>>();

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
            "Released availability block for aircraft {AircraftId} (completed Booking: {BookingId})",
            message.AircraftId, message.BookingId);
    }
}
