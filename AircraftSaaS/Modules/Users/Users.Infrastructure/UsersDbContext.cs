using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Shared.Kernel.Domain;
using Users.Domain.Entities;
using Users.Domain.Identity;

namespace Users.Infrastructure;

internal class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid, IdentityUserClaim<Guid>, AppUserRole, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    // Current tenant ID for query filters (set by application)
    public Guid? TenantId { get; set; }

    // Identity
    public DbSet<AppUserCompany> AppUserCompanies { get; set; } = default!;
    
    // Companies & Persons
    public DbSet<Company> Companies { get; set; } = default!;
    public DbSet<Person> Persons { get; set; } = default!;
    public DbSet<Contact> Contacts { get; set; } = default!;
    public DbSet<ContactType> ContactTypes { get; set; } = default!;
    
    // Licenses
    public DbSet<License> Licenses { get; set; } = default!;
    public DbSet<PilotLicenseType> PilotLicenseTypes { get; set; } = default!;
    
    // Audit
    public DbSet<AuditLog> AuditLogs { get; set; } = default!;
    
    // Auth tokens
    public DbSet<AppRefreshToken> RefreshTokens { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Each module owns its own schema for data isolation
        builder.HasDefaultSchema("users");

        // Disable cascade delete
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
            var property = Expression.Property(parameter, nameof(IBaseEntitySoftDelete.DeletedAt));
            var isNull = Expression.Equal(property, Expression.Constant(null, typeof(DateTime?)));
            var lambda = Expression.Lambda(isNull, parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }

        // ── LangStr value comparers (shared) ──────────────────────────────
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

        // ── ContactType.ContactTypeName ───────────────────────────────────
        var contactTypeNameBuilder = builder.Entity<ContactType>().Property(e => e.ContactTypeName)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        contactTypeNameBuilder.Metadata.SetValueComparer(langStrComparer);

        // ── Company.CompanyName ───────────────────────────────────────────
        var companyNameBuilder = builder.Entity<Company>().Property(e => e.CompanyName)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        companyNameBuilder.Metadata.SetValueComparer(langStrComparer);

        // ── Company.Address (nullable) ────────────────────────────────────
        var companyAddressBuilder = builder.Entity<Company>().Property(e => e.Address)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("jsonb");
        companyAddressBuilder.Metadata.SetValueComparer(langStrNullableComparer);

        // ── License.LicenseType ───────────────────────────────────────────
        var licenseTypeBuilder = builder.Entity<License>().Property(e => e.LicenseType)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        licenseTypeBuilder.Metadata.SetValueComparer(langStrComparer);

        // ── License.IssuingAuthority ──────────────────────────────────────
        var licenseAuthorityBuilder = builder.Entity<License>().Property(e => e.IssuingAuthority)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!
            )
            .HasColumnType("jsonb");
        licenseAuthorityBuilder.Metadata.SetValueComparer(langStrComparer);

        // ── AppUserRole FK configuration ──────────────────────────────────
        builder.Entity<AppUserRole>(b =>
        {
            b.HasOne(ur => ur.AppUser).WithMany().HasForeignKey(ur => ur.UserId);
            b.HasOne(ur => ur.AppRole).WithMany().HasForeignKey(ur => ur.RoleId);
        });

        // ── AppUserCompany.Role is a computed alias — not a DB column ─────
        builder.Entity<AppUserCompany>().Ignore(auc => auc.Role);
    }
}
