using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Contracts.Common;
using Shared.Contracts.Users;
using Users.Application;
using Users.Application.Contracts;
using Users.Application.Interfaces;
using Users.Application.Services;
using Users.Domain.Identity;
using Users.Infrastructure.Repositories;
using Users.Infrastructure.Seeding;
using Users.Infrastructure.Services;

namespace Users.Infrastructure;

/// <summary>
/// Module registration for the Users module.
/// Registers DbContext, Identity, repositories, UOW, application services, and MediatR handlers.
/// </summary>
public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── DbContext ─────────────────────────────────────────────────────
        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("UsersConnection")));

        // ── ASP.NET Core Identity ─────────────────────────────────────────
        services.AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 1;
            })
            .AddEntityFrameworkStores<UsersDbContext>()
            .AddDefaultTokenProviders()
            .AddDefaultUI();

        // ── Unit of Work ──────────────────────────────────────────────────
        services.AddScoped<IUsersUOW, UsersUOW>();

        // ── Repositories (standalone — not via UOW) ──────────────────────
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // ── Application Services ──────────────────────────────────────────
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IAppUserCompanyService, AppUserCompanyService>();
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IContactTypeService, ContactTypeService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantContext>(sp => (ITenantContext)sp.GetRequiredService<ITenantService>());

        // ── Infrastructure Services ───────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISystemAdminUsersService, SystemAdminUsersService>();

        // ── Module API (cross-module contract) ────────────────────────────
        services.AddScoped<IUsersModuleApi, UsersModuleApi>();

        // ── MediatR handlers are auto-registered by scanning this assembly
        // (The host project should call: services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UsersModule).Assembly));)

        return services;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Public seeding / migration surface — keeps DbContext internal
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Blocks until the database is reachable, retrying up to <paramref name="maxRetries"/> times.
    /// </summary>
    public static void WaitForDatabase(IServiceProvider sp, Action<string> log, int maxRetries = 60)
    {
        var ctx = sp.GetRequiredService<UsersDbContext>();
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

                log($"Checked postgres db connection (attempt {retryCount}/{maxRetries}). Got: {message}");

                if (message.Contains("does not exist"))
                {
                    log("Applying migration, probably db is not there (but server is)");
                    return;
                }

                if (retryCount >= maxRetries)
                    throw;

                log("Waiting for db connection. Sleep 1 sec");
                Thread.Sleep(1000);
            }
        }
    }

    public static void DeleteDatabase(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<UsersDbContext>();
        UsersDataInit.DeleteDatabase(ctx);
    }

    public static void MigrateDatabase(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<UsersDbContext>();
        UsersDataInit.MigrateDatabase(ctx);
    }

    public static void SeedIdentity(IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        UsersDataInit.SeedIdentity(userManager, roleManager);
    }

    public static void SeedAppData(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<UsersDbContext>();
        UsersDataInit.SeedAppData(ctx);
    }

    public static void SeedAppUserCompanies(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<UsersDbContext>();
        UsersDataInit.SeedAppUserCompanies(ctx);
    }

    /// <summary>
    /// Returns a slug → CompanyId mapping for cross-module seeding.
    /// </summary>
    public static Dictionary<string, Guid> GetCompanySlugMapping(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<UsersDbContext>();
        return ctx.Companies.ToDictionary(c => c.Slug, c => c.Id);
    }
}
