using App.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests.Helpers;

/// <summary>
/// Factory that creates AppDbContext instances backed by a real PostgreSQL Testcontainer.
/// Implements IAsyncLifetime so xUnit manages the container lifecycle.
/// </summary>
public class TestDbContextFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private DbContextOptions<AppDbContext>? _options;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        // Create schema using EnsureCreated (applies the model without migrations)
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public AppDbContext CreateContext()
    {
        return new AppDbContext(_options!);
    }
}
