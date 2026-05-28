using Fleet.Application.Contracts;
using Fleet.Domain.Entities;
using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace Fleet.WebHost.Consumers;

public class BookingCreatedConsumer : RabbitMqConsumerBase<BookingCreatedMessage>
{
    public BookingCreatedConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingCreatedConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "booking.events",
            queue: "fleet.booking.created",
            routingKey: "booking.created")
    { }

    protected override async Task HandleMessageAsync(
        BookingCreatedMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var uow = sp.GetRequiredService<IFleetUOW>();
        var logger = sp.GetRequiredService<ILogger<BookingCreatedConsumer>>();

        logger.LogInformation(
            "Handling BookingCreatedMessage: BookingId={BookingId}, AircraftId={AircraftId}",
            message.BookingId, message.AircraftId);

        // Check if an availability block already exists for this booking
        var existing = await uow.AircraftAvailabilityRepository
            .GetByBookingIdTrackingAsync(message.BookingId);

        if (existing != null)
        {
            logger.LogWarning(
                "Availability block already exists for BookingId={BookingId}. Skipping.",
                message.BookingId);
            return;
        }

        var availability = new AircraftAvailability
        {
            AircraftId = message.AircraftId,
            BookingId = message.BookingId,
            StartDateTime = DateTime.SpecifyKind(message.StartDateTime, DateTimeKind.Utc),
            EndDateTime = DateTime.SpecifyKind(message.EndDateTime, DateTimeKind.Utc),
            AvailabilityType = "Booked",
            Reason = $"Booking {message.BookingId} by pilot {message.PilotId}"
        };

        uow.AircraftAvailabilityRepository.Add(availability);
        await uow.SaveChangesAsync();

        logger.LogInformation(
            "Created Booked availability block for aircraft {AircraftId} (Booking: {BookingId})",
            message.AircraftId, message.BookingId);
    }
}
