using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using App.Domain;
using App.Domain.Entities;
using App.Domain.Identity;
using Base.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid, IdentityUserClaim<Guid>, AppUserRole, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options), IDataProtectionKeyContext
{
    // Current tenant ID for query filters (set by application)
    public Guid? TenantId { get; set; }

    // Identity
    public DbSet<AppUserCompany> AppUserCompanies { get; set; }
    
    // Companies & Persons
    public DbSet<Company> Companies { get; set; }
    public DbSet<Person> Persons { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<ContactType> ContactTypes { get; set; }
    
    // Aircraft Rental Domain
    public DbSet<Aircraft> Aircraft { get; set; }
    public DbSet<Airport> Airports { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<License> Licenses { get; set; }
    public DbSet<InsurancePolicy> InsurancePolicies { get; set; }
    public DbSet<AircraftPhoto> AircraftPhotos { get; set; }
    public DbSet<AircraftAvailability> AircraftAvailabilities { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    
    public DbSet<AppRefreshToken> RefreshTokens { get; set; } = default!;

    // This maps to the table that stores data protection keys.
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // disable cascade delete
        foreach (var relationship in builder.Model
                     .GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Apply global soft-delete query filters to all entities implementing ISoftDelete.
        // Any query against these tables will automatically exclude records where DeletedAt IS NOT NULL.
        // Use .IgnoreQueryFilters() on a query when you intentionally need deleted records (e.g. admin restore).
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


        // LangStr value comparer (shared)
        var langStrComparer = new ValueComparer<LangStr>(
            (v1, v2) => v1!.SequenceEqual(v2!),
            v => v.Aggregate(0, (hash, keyValue) => HashCode.Combine(hash, keyValue.Key.GetHashCode(), keyValue.Value.GetHashCode())),
            v => new LangStr(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null))
        );
        var langStrNullableComparer = new ValueComparer<LangStr?>(
            (v1, v2) => v1 == null ? v2 == null : v2 != null && v1.SequenceEqual(v2),
            v => v == null ? 0 : v.Aggregate(0, (hash, keyValue) => HashCode.Combine(hash, keyValue.Key.GetHashCode(), keyValue.Value.GetHashCode())),
            v => v == null ? null : new LangStr(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null))
        );

        // ContactType.ContactTypeName
        var contactTypeNameBuilder = builder.Entity<ContactType>().Property(e => e.ContactTypeName)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        contactTypeNameBuilder.Metadata.SetValueComparer(langStrComparer);

        // Aircraft.Description
        var aircraftDescBuilder = builder.Entity<Aircraft>().Property(e => e.Description)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        aircraftDescBuilder.Metadata.SetValueComparer(langStrComparer);

        // Aircraft.Category
        var aircraftCatBuilder = builder.Entity<Aircraft>().Property(e => e.Category)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        aircraftCatBuilder.Metadata.SetValueComparer(langStrComparer);

        // Aircraft.Make
        var aircraftMakeBuilder = builder.Entity<Aircraft>().Property(e => e.Make)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        aircraftMakeBuilder.Metadata.SetValueComparer(langStrComparer);

        // Aircraft.Model
        var aircraftModelBuilder = builder.Entity<Aircraft>().Property(e => e.Model)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        aircraftModelBuilder.Metadata.SetValueComparer(langStrComparer);

        // Airport.Name
        var airportNameBuilder = builder.Entity<Airport>().Property(e => e.Name)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        airportNameBuilder.Metadata.SetValueComparer(langStrComparer);

        // Airport.City
        var airportCityBuilder = builder.Entity<Airport>().Property(e => e.City)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        airportCityBuilder.Metadata.SetValueComparer(langStrComparer);

        // Airport.Country
        var airportCountryBuilder = builder.Entity<Airport>().Property(e => e.Country)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        airportCountryBuilder.Metadata.SetValueComparer(langStrComparer);

        // MaintenanceRecord.MaintenanceType
        var maintTypeBuilder = builder.Entity<MaintenanceRecord>().Property(e => e.MaintenanceType)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        maintTypeBuilder.Metadata.SetValueComparer(langStrComparer);

        // MaintenanceRecord.Status
        var maintStatusBuilder = builder.Entity<MaintenanceRecord>().Property(e => e.Status)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        maintStatusBuilder.Metadata.SetValueComparer(langStrComparer);

        // MaintenanceRecord.Description
        var maintDescBuilder = builder.Entity<MaintenanceRecord>().Property(e => e.Description)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        maintDescBuilder.Metadata.SetValueComparer(langStrComparer);

        // Company.CompanyName
        var companyNameBuilder = builder.Entity<Company>().Property(e => e.CompanyName)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        companyNameBuilder.Metadata.SetValueComparer(langStrComparer);

        // Company.Address (nullable)
        var companyAddressBuilder = builder.Entity<Company>().Property(e => e.Address)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        companyAddressBuilder.Metadata.SetValueComparer(langStrNullableComparer);

        // Review.Comment (nullable)
        var reviewCommentBuilder = builder.Entity<Review>().Property(e => e.Comment)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        reviewCommentBuilder.Metadata.SetValueComparer(langStrNullableComparer);

        // Review.ReviewType (nullable)
        var reviewTypeBuilder = builder.Entity<Review>().Property(e => e.ReviewType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        reviewTypeBuilder.Metadata.SetValueComparer(langStrNullableComparer);

        // Booking.Purpose (nullable)
        var bookingPurposeBuilder = builder.Entity<Booking>().Property(e => e.Purpose)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        bookingPurposeBuilder.Metadata.SetValueComparer(langStrNullableComparer);

        // Booking.RejectionReason (nullable)
        var bookingRejectionBuilder = builder.Entity<Booking>().Property(e => e.RejectionReason)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        bookingRejectionBuilder.Metadata.SetValueComparer(langStrNullableComparer);

        // License.LicenseType
        var licensetypeBuilder = builder.Entity<License>().Property(e => e.LicenseType)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        licensetypeBuilder.Metadata.SetValueComparer(langStrComparer);

        // License.IssuingAuthority
        var licenseAuthorityBuilder = builder.Entity<License>().Property(e => e.IssuingAuthority)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        licenseAuthorityBuilder.Metadata.SetValueComparer(langStrComparer);

        // InsurancePolicy.InsuranceProvider
        var insuranceProviderBuilder = builder.Entity<InsurancePolicy>().Property(e => e.InsuranceProvider)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        insuranceProviderBuilder.Metadata.SetValueComparer(langStrComparer);

        // InsurancePolicy.CoverageType
        var coverageTypeBuilder = builder.Entity<InsurancePolicy>().Property(e => e.CoverageType)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        coverageTypeBuilder.Metadata.SetValueComparer(langStrComparer);

        // Configure Booking -> Payment relationship
        builder.Entity<Booking>()
            .HasMany(b => b.Payments)
            .WithOne(p => p.Booking)
            .HasForeignKey(p => p.BookingId);

        // 6A.1: Booking.AppUserId is a computed alias for PilotId — not a DB column
        builder.Entity<Booking>().Ignore(b => b.AppUserId);

        // 6A.1b: Review.AppUserId is a computed alias for AuthorId — not a DB column
        builder.Entity<Review>().Ignore(r => r.AppUserId);

        // 6A.2: AppUserRole FK configuration (replaces [ForeignKey] attributes on domain entity)
        builder.Entity<AppUserRole>(b =>
        {
            b.HasOne(ur => ur.AppUser).WithMany().HasForeignKey(ur => ur.UserId);
            b.HasOne(ur => ur.AppRole).WithMany().HasForeignKey(ur => ur.RoleId);
        });

        // 6A.3: AppUserCompany.Role is a computed alias for AppUserRoleInCompany — not a DB column
        builder.Entity<AppUserCompany>().Ignore(auc => auc.Role);

    }
}

