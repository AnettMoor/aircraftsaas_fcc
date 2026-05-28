using System.Linq.Expressions;
using System.Text.Json;
using Fleet.Domain.Entities;
using Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Fleet.Infrastructure;

internal class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options)
{
    // Fleet-owned DbSets
    public DbSet<Aircraft> Aircrafts { get; set; } = default!;
    public DbSet<AircraftPhoto> AircraftPhotos { get; set; } = default!;
    public DbSet<AircraftAvailability> AircraftAvailabilities { get; set; } = default!;
    public DbSet<Airport> Airports { get; set; } = default!;
    public DbSet<InsurancePolicy> InsurancePolicies { get; set; } = default!;
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Each module owns its own schema for data isolation
        builder.HasDefaultSchema("fleet");

        // Disable cascade delete for all relationships
        foreach (var relationship in builder.Model
                     .GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // ── Global soft-delete query filters ──
        // Automatically exclude records where DeletedAt IS NOT NULL.
        // Use .IgnoreQueryFilters() when you intentionally need deleted records.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDelete.DeletedAt));
            var isNull = Expression.Equal(property, Expression.Constant(null, typeof(DateTime?)));
            var lambda = Expression.Lambda(isNull, parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }

        // ── LangStr value comparers (shared) ──
        var langStrComparer = new ValueComparer<LangStr>(
            (v1, v2) => v1!.SequenceEqual(v2!),
            v => v.Aggregate(0, (hash, keyValue) =>
                HashCode.Combine(hash, keyValue.Key.GetHashCode(), keyValue.Value.GetHashCode())),
            v => new LangStr(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null))
        );

        // ── Aircraft LangStr properties ──
        ConfigureLangStr<Aircraft>(builder, e => e.Make, langStrComparer);
        ConfigureLangStr<Aircraft>(builder, e => e.Model, langStrComparer);
        ConfigureLangStr<Aircraft>(builder, e => e.Category, langStrComparer);
        ConfigureLangStr<Aircraft>(builder, e => e.Description, langStrComparer);

        // ── Airport LangStr properties ──
        ConfigureLangStr<Airport>(builder, e => e.Name, langStrComparer);
        ConfigureLangStr<Airport>(builder, e => e.City, langStrComparer);
        ConfigureLangStr<Airport>(builder, e => e.Country, langStrComparer);

        // ── MaintenanceRecord LangStr properties ──
        ConfigureLangStr<MaintenanceRecord>(builder, e => e.MaintenanceType, langStrComparer);
        ConfigureLangStr<MaintenanceRecord>(builder, e => e.Status, langStrComparer);
        ConfigureLangStr<MaintenanceRecord>(builder, e => e.Description, langStrComparer);

        // ── InsurancePolicy LangStr properties ──
        ConfigureLangStr<InsurancePolicy>(builder, e => e.InsuranceProvider, langStrComparer);
        ConfigureLangStr<InsurancePolicy>(builder, e => e.CoverageType, langStrComparer);

        // ── Computed properties — not mapped to DB ──
        builder.Entity<InsurancePolicy>().Ignore(e => e.IsActive);
        builder.Entity<AircraftPhoto>().Ignore(e => e.Url);
        builder.Entity<Aircraft>().Ignore(e => e.IsDeleted);
        builder.Entity<AircraftPhoto>().Ignore(e => e.IsDeleted);
        builder.Entity<AircraftAvailability>().Ignore(e => e.IsDeleted);
        builder.Entity<Airport>().Ignore(e => e.IsDeleted);
        builder.Entity<InsurancePolicy>().Ignore(e => e.IsDeleted);
        builder.Entity<MaintenanceRecord>().Ignore(e => e.IsDeleted);

        // ── Same-module relationships ──

        // Aircraft → BaseAirport (same-module navigation)
        builder.Entity<Aircraft>()
            .HasOne(a => a.BaseAirport)
            .WithMany(ap => ap.Aircraft)
            .HasForeignKey(a => a.BaseAirportId);

        // Aircraft → AircraftPhotos
        builder.Entity<Aircraft>()
            .HasMany(a => a.Photos)
            .WithOne(p => p.Aircraft)
            .HasForeignKey(p => p.AircraftId);

        // Aircraft → AircraftAvailabilities
        builder.Entity<Aircraft>()
            .HasMany(a => a.Availabilities)
            .WithOne(av => av.Aircraft)
            .HasForeignKey(av => av.AircraftId);

        // Aircraft → InsurancePolicies
        builder.Entity<Aircraft>()
            .HasMany(a => a.InsurancePolicies)
            .WithOne(ip => ip.Aircraft)
            .HasForeignKey(ip => ip.AircraftId);

        // Aircraft → MaintenanceRecords
        builder.Entity<Aircraft>()
            .HasMany(a => a.MaintenanceRecords)
            .WithOne(mr => mr.Aircraft)
            .HasForeignKey(mr => mr.AircraftId);

        // ── Cross-module FK columns — index only, NO navigation ──

        // Aircraft.CompanyId — cross-module FK to Users.Company
        builder.Entity<Aircraft>()
            .HasIndex(a => a.CompanyId);

        // AircraftAvailability.BookingId — cross-module FK to Booking
        builder.Entity<AircraftAvailability>()
            .HasIndex(av => av.BookingId);

        // MaintenanceRecord.PerformedByUserId — cross-module FK to Users.AppUser
        builder.Entity<MaintenanceRecord>()
            .HasIndex(mr => mr.PerformedByUserId);

        // ── Unique indexes ──
        builder.Entity<Aircraft>()
            .HasIndex(a => a.RegistrationNumber)
            .IsUnique();

        builder.Entity<Airport>()
            .HasIndex(a => a.IcaoCode)
            .IsUnique();
    }

    /// <summary>
    /// Helper to configure a LangStr property as JSONB with the shared comparer.
    /// </summary>
    private static void ConfigureLangStr<TEntity>(
        ModelBuilder builder,
        Expression<Func<TEntity, LangStr>> propertyExpression,
        ValueComparer<LangStr> comparer)
        where TEntity : class
    {
        var propBuilder = builder.Entity<TEntity>().Property(propertyExpression)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        propBuilder.Metadata.SetValueComparer(comparer);
    }
}
