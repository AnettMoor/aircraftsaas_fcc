using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Seeding;

/// <summary>
/// Booking module data initialization.
/// Booking data is typically created at runtime (by users making bookings),
/// so seeding is minimal — mainly database migration and optional test data.
/// </summary>
internal static class BookingDataInit
{
    public static void MigrateDatabase(BookingDbContext context)
    {
        context.Database.Migrate();
    }

    public static void SeedBookingData(BookingDbContext context)
    {
        // Booking-related data is created by users at runtime.
        // Add any required seed data here (e.g., test bookings for development).

        context.SaveChanges();
    }
}
