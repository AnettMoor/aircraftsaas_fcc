using Fleet.Application;
using Fleet.Application.Contracts;
using Fleet.Application.Interfaces;
using Fleet.Application.Services;
using Fleet.Infrastructure.Repositories;
using Fleet.Infrastructure.Seeding;
using Fleet.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Contracts.Fleet;

namespace Fleet.Infrastructure;

/// <summary>
/// Module registration for the Fleet module.
/// Registers DbContext, repositories, UOW, application services, and MediatR handlers.
/// </summary>
public static class FleetModule
{
    public static IServiceCollection AddFleetModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── DbContext ─────────────────────────────────────────────────────
        services.AddDbContext<FleetDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("FleetConnection")));

        // ── Unit of Work ──────────────────────────────────────────────────
        services.AddScoped<IFleetUOW, FleetUOW>();

        // ── Repositories (standalone — for direct injection if needed) ────
        services.AddScoped<IAircraftRepository, AircraftRepository>();
        services.AddScoped<IAircraftAvailabilityRepository, AircraftAvailabilityRepository>();
        services.AddScoped<IAirportRepository, AirportRepository>();
        services.AddScoped<IInsurancePolicyRepository, InsurancePolicyRepository>();
        services.AddScoped<IMaintenanceRecordRepository, MaintenanceRecordRepository>();

        // ── Application Services ──────────────────────────────────────────
        services.AddScoped<IAircraftService, AircraftService>();
        services.AddScoped<IAircraftAvailabilityService, AircraftAvailabilityService>();
        services.AddScoped<IAirportService, AirportService>();
        services.AddScoped<IInsurancePolicyService, InsurancePolicyService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();

        // ── Infrastructure Services ───────────────────────────────────────
        services.AddScoped<ISystemAdminFleetService, SystemAdminFleetService>();

        // ── Module API (cross-module contract) ────────────────────────────
        services.AddScoped<IFleetModuleApi, FleetModuleApi>();

        // ── MediatR handlers are auto-registered by scanning this assembly
        // (The host project should call: services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(FleetModule).Assembly));)

        return services;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Public seeding / migration surface — keeps DbContext internal
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Blocks until the Fleet database is reachable, retrying up to <paramref name="maxRetries"/> times.
    /// </summary>
    public static void WaitForDatabase(IServiceProvider sp, Action<string> log, int maxRetries = 60)
    {
        var ctx = sp.GetRequiredService<FleetDbContext>();
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

                log($"[Fleet] Checked db connection (attempt {retryCount}/{maxRetries}). Got: {message}");

                if (message.Contains("does not exist"))
                {
                    log("[Fleet] Applying migration, probably db is not there (but server is)");
                    return;
                }

                if (retryCount >= maxRetries)
                    throw;

                log("[Fleet] Waiting for db connection. Sleep 1 sec");
                Thread.Sleep(1000);
            }
        }
    }

    public static void DeleteDatabase(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<FleetDbContext>();
        ctx.Database.EnsureDeleted();
    }

    public static void MigrateDatabase(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<FleetDbContext>();
        FleetDataInit.MigrateDatabase(ctx);
    }

    /// <summary>
    /// Seeds Fleet data. <paramref name="companyBySlug"/> provides the cross-module
    /// company slug → Guid mapping obtained from <c>UsersModule.GetCompanySlugMapping</c>.
    /// </summary>
    public static void SeedFleetData(IServiceProvider sp, Dictionary<string, Guid> companyBySlug)
    {
        var ctx = sp.GetRequiredService<FleetDbContext>();
        FleetDataInit.SeedFleetData(ctx, companyBySlug);
    }
}
