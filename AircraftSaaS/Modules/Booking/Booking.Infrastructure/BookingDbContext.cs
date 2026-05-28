using System.Linq.Expressions;
using System.Text.Json;
using Booking.Domain.Entities;
using Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Booking.Infrastructure;

internal class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    // Booking-owned DbSets
    public DbSet<Domain.Entities.Booking> Bookings { get; set; } = default!;
    public DbSet<Payment> Payments { get; set; } = default!;
    public DbSet<Review> Reviews { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Each module owns its own schema for data isolation
        builder.HasDefaultSchema("booking");

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

        var langStrNullableComparer = new ValueComparer<LangStr?>(
            (v1, v2) => v1 == null ? v2 == null : v2 != null && v1.SequenceEqual(v2),
            v => v == null ? 0 : v.Aggregate(0, (hash, keyValue) =>
                HashCode.Combine(hash, keyValue.Key.GetHashCode(), keyValue.Value.GetHashCode())),
            v => v == null ? null : new LangStr(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null))
        );

        // ── Booking LangStr properties (nullable) ──
        ConfigureNullableLangStr<Domain.Entities.Booking>(builder, e => e.Purpose, langStrNullableComparer);
        ConfigureNullableLangStr<Domain.Entities.Booking>(builder, e => e.RejectionReason, langStrNullableComparer);

        // ── Review LangStr properties (nullable) ──
        ConfigureNullableLangStr<Review>(builder, e => e.Comment, langStrNullableComparer);
        ConfigureNullableLangStr<Review>(builder, e => e.ReviewType, langStrNullableComparer);

        // ── Computed/derived properties — not mapped to DB ──
        builder.Entity<Domain.Entities.Booking>().Ignore(e => e.IsDeleted);
        builder.Entity<Domain.Entities.Booking>().Ignore(e => e.AppUserId);
        builder.Entity<Payment>().Ignore(e => e.IsDeleted);
        builder.Entity<Review>().Ignore(e => e.IsDeleted);
        builder.Entity<Review>().Ignore(e => e.AppUserId);

        // ── Same-module relationships ──

        // Booking → Payments (one-to-many)
        builder.Entity<Domain.Entities.Booking>()
            .HasMany(b => b.Payments)
            .WithOne(p => p.Booking)
            .HasForeignKey(p => p.BookingId);

        // Booking → Reviews (one-to-many)
        builder.Entity<Domain.Entities.Booking>()
            .HasMany(b => b.Reviews)
            .WithOne(r => r.Booking)
            .HasForeignKey(r => r.BookingId);

        // ── Cross-module FK columns — index only, NO navigation ──

        // Booking.AircraftId — cross-module FK to Fleet.Aircraft
        builder.Entity<Domain.Entities.Booking>()
            .HasIndex(b => b.AircraftId);

        // Booking.PilotId — cross-module FK to Users.AppUser
        builder.Entity<Domain.Entities.Booking>()
            .HasIndex(b => b.PilotId);

        // Booking.CompanyId — cross-module FK to Users.Company
        builder.Entity<Domain.Entities.Booking>()
            .HasIndex(b => b.CompanyId);

        // Booking.CustomerId — cross-module FK to Users.AppUser (optional)
        builder.Entity<Domain.Entities.Booking>()
            .HasIndex(b => b.CustomerId);

        // Review.AircraftId — cross-module FK to Fleet.Aircraft
        builder.Entity<Review>()
            .HasIndex(r => r.AircraftId);

        // Review.AuthorId — cross-module FK to Users.AppUser
        builder.Entity<Review>()
            .HasIndex(r => r.AuthorId);
    }

    /// <summary>
    /// Helper to configure a nullable LangStr property as JSONB with the shared comparer.
    /// </summary>
    private static void ConfigureNullableLangStr<TEntity>(
        ModelBuilder builder,
        Expression<Func<TEntity, LangStr?>> propertyExpression,
        ValueComparer<LangStr?> comparer)
        where TEntity : class
    {
        var propBuilder = builder.Entity<TEntity>().Property(propertyExpression)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        propBuilder.Metadata.SetValueComparer(comparer);
    }
}
