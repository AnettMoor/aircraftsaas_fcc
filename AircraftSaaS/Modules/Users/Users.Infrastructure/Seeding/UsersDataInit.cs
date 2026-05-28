using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Domain;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Domain.Identity;

namespace Users.Infrastructure.Seeding;

internal static class UsersDataInit
{
    // ── Static seed data ─────────────────────────────────────────────────

    public static readonly string[] Roles = ["Normal", "CompanyOwner", "SystemAdmin"];

    public static readonly (string email, string password, string[] roles)[] Users =
    [
        ("1@3", "3", ["Normal"]),
        ("1@2", "2", ["CompanyOwner"]),
        ("1@4", "4", ["SystemAdmin"])
    ];

    public static readonly (string En, string Et)[] ContactTypesWithEt =
    [
        ("email", "e-post"),
        ("post", "post"),
        ("phone", "telefon")
    ];

    public static readonly CompanySeedData[] Companies =
    [
        new("Baltic Air Charter", "Baltic Air Charter", "baltic-air-charter", 10, 20, 100,
            "Lennujaama tee 12, Tallinn 11101, Estonia", "+372 605 8888", "info@balticaircharter.ee"),
        new("Nordic Wings OÜ", "Nordic Wings OÜ", "nordic-wings", 999, 999, 9999,
            "Lentäjäntie 3, 01530 Vantaa, Finland", "+358 9 123 4567", "ops@nordicwings.fi"),
        new("Riga Flight Services", "Riia lennusteenused", "riga-flight-services", 2, 3, 20,
            "Lidosta Riga 10/1, Marupes novads, LV-1053, Latvia", "+371 6720 7009", "contact@rigaflight.lv")
    ];

    public record CompanySeedData(
        string Name, string NameEt, string Slug,
        int MaxUsers, int MaxAircraft, int MaxBookingsPerMonth,
        string Address, string Phone, string Email);

    // ── Database operations ──────────────────────────────────────────────

    public static void DeleteDatabase(UsersDbContext context)
    {
        context.Database.EnsureDeleted();
    }

    public static void MigrateDatabase(UsersDbContext context)
    {
        context.Database.Migrate();
    }

    // ── Seed Identity (roles + users) ────────────────────────────────────

    public static void SeedIdentity(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
        foreach (var roleName in Roles)
        {
            var role = roleManager.FindByNameAsync(roleName).Result;
            if (role != null) continue;

            role = new AppRole { Name = roleName };
            var result = roleManager.CreateAsync(role).Result;
            if (!result.Succeeded)
            {
                throw new ApplicationException("Role creation failed!");
            }
        }

        foreach (var userInfo in Users)
        {
            var user = userManager.FindByEmailAsync(userInfo.email).Result;
            if (user == null)
            {
                user = new AppUser
                {
                    Email = userInfo.email,
                    UserName = userInfo.email,
                    EmailConfirmed = true
                };
                var result = userManager.CreateAsync(user, userInfo.password).Result;
                if (!result.Succeeded)
                {
                    throw new ApplicationException("User creation failed!");
                }
            }

            foreach (var role in userInfo.roles)
            {
                if (userManager.IsInRoleAsync(user, role).Result)
                {
                    Console.WriteLine($"User {user.UserName} already in role {role}");
                    continue;
                }

                var roleResult = userManager.AddToRoleAsync(user, role).Result;
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        Console.WriteLine(error.Description);
                    }
                }
                else
                {
                    Console.WriteLine($"User {user.UserName} added to role {role}");
                }
            }
        }
    }

    // ── Seed Users-module app data (contact types + companies) ───────────

    public static void SeedAppData(UsersDbContext context)
    {
        // -- Contact types ------------------------------------------------
        var existingContactTypes = context.ContactTypes.ToList();
        foreach (var (en, et) in ContactTypesWithEt)
        {
            if (!existingContactTypes.Any(ct => ct.ContactTypeName.ToString() == en))
            {
                var name = new LangStr(en, "en");
                name.SetTranslation(et, "et");
                context.ContactTypes.Add(new ContactType
                {
                    ContactTypeName = name,
                    CreatedBy = "system"
                });
            }
        }

        context.SaveChanges();

        // -- Companies ----------------------------------------------------
        foreach (var companyData in Companies)
        {
            var existingCompany = context.Companies.FirstOrDefault(c => c.Slug == companyData.Slug);
            if (existingCompany == null)
            {
                var companyName = new LangStr(companyData.Name, "en");
                companyName.SetTranslation(companyData.NameEt, "et");

                context.Companies.Add(new Company
                {
                    CompanyName = companyName,
                    Slug = companyData.Slug,
                    IsActive = true,
                    MaxUsers = companyData.MaxUsers,
                    MaxAircraft = companyData.MaxAircraft,
                    MaxBookingsPerMonth = companyData.MaxBookingsPerMonth,
                    Address = companyData.Address,
                    Phone = companyData.Phone,
                    Email = companyData.Email,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
            }
        }

        context.SaveChanges();
    }

    // ── Seed AppUserCompany associations ─────────────────────────────────

    public static void SeedAppUserCompanies(UsersDbContext context)
    {
        // Use the first seeded company (Baltic Air Charter) as the default company for users
        var defaultCompany = context.Companies.FirstOrDefault(c => c.Slug == "baltic-air-charter");

        // Fallback: create a generic default company if no seeded companies exist
        if (defaultCompany == null)
        {
            defaultCompany = context.Companies.ToList()
                .FirstOrDefault(c => c.CompanyName.ToString() == "Default Company");

            if (defaultCompany == null)
            {
                defaultCompany = new Company
                {
                    CompanyName = new LangStr("Default Company"),
                    Slug = "default-company",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };
                context.Companies.Add(defaultCompany);
                context.SaveChanges();
            }
        }

        // Ensure existing company is active
        if (!defaultCompany.IsActive)
        {
            defaultCompany.IsActive = true;
            context.SaveChanges();
        }

        // Get all users and ensure non-Normal users have an AppUserCompany record.
        // Normal users are NOT associated with any company.
        var users = context.Users.ToList();
        foreach (var user in users)
        {
            // Determine the user's Identity role from seed data
            var userInfo = Users.FirstOrDefault(u => u.email == user.Email);
            string identityRole = userInfo.email != null
                ? (userInfo.roles?.FirstOrDefault() ?? "Normal")
                : "Normal";

            // Skip Normal users — they should not be associated with a company
            if (identityRole == "Normal")
            {
                Console.WriteLine($"Skipping Normal user {user.Email} — Normal users are not associated with companies");
                continue;
            }

            var existingMembership = context.AppUserCompanies
                .FirstOrDefault(uc => uc.AppUserId == user.Id && uc.CompanyId == defaultCompany.Id);

            if (existingMembership == null)
            {
                EAppUserRoleInCompany companyRole = identityRole switch
                {
                    "CompanyOwner" => EAppUserRoleInCompany.CompanyOwner,
                    "SystemAdmin" => EAppUserRoleInCompany.SystemAdmin,
                    _ => EAppUserRoleInCompany.CompanyOwner // fallback; Normal is excluded above
                };

                context.AppUserCompanies.Add(new AppUserCompany
                {
                    AppUserId = user.Id,
                    CompanyId = defaultCompany.Id,
                    AppUserRoleInCompany = companyRole,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
                Console.WriteLine($"Added user {user.Email} to {defaultCompany.Slug} with {companyRole} role");
            }
        }

        context.SaveChanges();
    }
}
