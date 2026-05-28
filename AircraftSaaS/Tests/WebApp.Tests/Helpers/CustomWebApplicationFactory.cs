using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Users.Infrastructure;

namespace WebApp.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that uses Testcontainers to spin up a real PostgreSQL
/// container for integration testing. After the Fleet/Booking microservice extraction,
/// WebApp only owns the Users database for Identity cookie auth. Fleet and Booking
/// databases are managed by their respective microservices.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private string _usersConnectionString = default!;

    /// <summary>
    /// Called by xUnit before any test class constructor. Starts the PostgreSQL container
    /// and creates the Users database.
    /// </summary>
    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        var baseConnStr = _postgres.GetConnectionString();
        _usersConnectionString = ReplaceDatabase(baseConnStr, "aircraft_users_test");

        await using var conn = new NpgsqlConnection(baseConnStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE DATABASE \"aircraft_users_test\"";
        await cmd.ExecuteNonQueryAsync();
    }

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

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UsersConnection"] = _usersConnectionString,
                ["DataInitialization:DropDatabase"] = "false",
                ["DataInitialization:MigrateDatabase"] = "true",
                ["DataInitialization:SeedIdentity"] = "true",
                ["DataInitialization:SeedData"] = "true",
                ["DataInitialization:SeedAppUserCompanies"] = "true",
                // Fleet and Booking are now separate microservices — provide dummy URLs
                ["FleetService:BaseUrl"] = "http://localhost:5002",
                ["BookingService:BaseUrl"] = "http://localhost:5003",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove and re-add UsersDbContext
            var usersDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<UsersDbContext>));
            if (usersDescriptor != null)
                services.Remove(usersDescriptor);

            services.AddDbContext<UsersDbContext>(options =>
                options.UseNpgsql(_usersConnectionString));
        });
    }

    /// <summary>
    /// Replaces the Database component of a PostgreSQL connection string.
    /// </summary>
    private static string ReplaceDatabase(string connectionString, string newDatabase)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = newDatabase
        };
        return csb.ConnectionString;
    }
}
