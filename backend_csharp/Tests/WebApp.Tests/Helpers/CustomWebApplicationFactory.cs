using App.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace WebApp.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that uses Testcontainers to spin up a real PostgreSQL
/// container for integration testing. Replaces the DbContext options via ConfigureTestServices
/// to ensure the app uses the test container's connection string (not the local one from appsettings.json).
/// 
/// Why ConfigureTestServices? In minimal hosting, Program.cs reads the connection string from
/// builder.Configuration at startup (line 29), BEFORE ConfigureAppConfiguration overrides are applied.
/// ConfigureTestServices runs AFTER all service registrations but before the service provider is built,
/// so it correctly overrides the DbContextOptions.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    /// <summary>
    /// Called by xUnit before any test class constructor. Starts the PostgreSQL container.
    /// </summary>
    Task IAsyncLifetime.InitializeAsync() => _postgres.StartAsync();

    /// <summary>
    /// Called by xUnit after all tests complete. Stops container and disposes factory.
    /// </summary>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override configuration settings. The ConnectionStrings override is needed for any code
        // that reads IConfiguration directly (e.g., SetupAppData -> WaitDbConnection if it used config).
        // DataInitialization settings are read after Build(), so they DO pick up this override.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["DataInitialization:DropDatabase"] = "false",
                ["DataInitialization:MigrateDatabase"] = "true",
                ["DataInitialization:SeedIdentity"] = "true",
                ["DataInitialization:SeedData"] = "true",
                ["DataInitialization:SeedAppUserCompanies"] = "true",
            });
        });

        // CRITICAL: Replace the DbContext options to use the Testcontainers connection string.
        // Program.cs reads the connection string at startup (before ConfigureAppConfiguration applies),
        // so the original DbContext would point to the local PostgreSQL. We must replace it here.
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContextOptions<AppDbContext> registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Re-add with Testcontainers connection string (same Npgsql provider — no dual-provider conflict)
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }
}
