using Booking.Application;
using Booking.Application.Contracts;
using Booking.Application.Interfaces;
using Booking.Application.Services;
using Booking.Infrastructure.Repositories;
using Booking.Infrastructure.Seeding;
using Booking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Contracts.Booking;

namespace Booking.Infrastructure;

/// <summary>
/// Module registration for the Booking module.
/// Registers DbContext, repositories, UOW, application services, and MediatR handlers.
/// </summary>
public static class BookingModule
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── DbContext ─────────────────────────────────────────────────────
        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("BookingConnection")));

        // ── Unit of Work ──────────────────────────────────────────────────
        services.AddScoped<IBookingUOW, BookingUOW>();

        // ── Repositories (standalone — for direct injection if needed) ────
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();

        // ── Application Services ──────────────────────────────────────────
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IReviewService, ReviewService>();

        // ── Infrastructure Services ───────────────────────────────────────
        services.AddScoped<ISystemAdminBookingService, SystemAdminBookingService>();

        // ── Module API (cross-module contract) ────────────────────────────
        services.AddScoped<IBookingModuleApi, BookingModuleApi>();

        // ── MediatR handlers are auto-registered by scanning this assembly
        // (The host project should call: services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(BookingModule).Assembly));)

        return services;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Public seeding / migration surface — keeps DbContext internal
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Blocks until the Booking database is reachable, retrying up to <paramref name="maxRetries"/> times.
    /// </summary>
    public static void WaitForDatabase(IServiceProvider sp, Action<string> log, int maxRetries = 60)
    {
        var ctx = sp.GetRequiredService<BookingDbContext>();
        var retryCount = 0;

        while (true)
        {
            try
            {
                ctx.Database.OpenConnection();
                ctx.Database.CloseConnection();
                return;
            }
            catch (Exception e)
            {
                retryCount++;
                var pgEx = e as PostgresException ?? e.InnerException as PostgresException;
                var message = pgEx?.Message ?? e.InnerException?.Message ?? e.Message;

                log($"[Booking] Checked db connection (attempt {retryCount}/{maxRetries}). Got: {message}");

                if (message.Contains("does not exist"))
                {
                    log("[Booking] Applying migration, probably db is not there (but server is)");
                    return;
                }

                if (retryCount >= maxRetries)
                    throw;

                log("[Booking] Waiting for db connection. Sleep 1 sec");
                Thread.Sleep(1000);
            }
        }
    }

    public static void DeleteDatabase(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<BookingDbContext>();
        ctx.Database.EnsureDeleted();
    }

    public static void MigrateDatabase(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<BookingDbContext>();
        BookingDataInit.MigrateDatabase(ctx);
    }

    public static void SeedBookingData(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<BookingDbContext>();
        BookingDataInit.SeedBookingData(ctx);
    }
}
