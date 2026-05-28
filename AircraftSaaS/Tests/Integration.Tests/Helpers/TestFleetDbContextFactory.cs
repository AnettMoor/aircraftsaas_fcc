using Fleet.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Integration.Tests.Helpers;

/// <summary>
/// Factory that creates FleetDbContext instances backed by a real PostgreSQL Testcontainer.
/// Implements IAsyncLifetime so xUnit manages the container lifecycle.
/// </summary>
internal class TestFleetDbContextFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private DbContextOptions<FleetDbContext>? _options;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        // Create schema using EnsureCreated (applies the model without migrations)
        await using var context = new FleetDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public FleetDbContext CreateContext()
    {
        return new FleetDbContext(_options!);
    }
}
