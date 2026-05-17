using App.Domain;
using App.Domain.Identity;
using App.Domain.Entities;
using Base.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Seeding;

public static class AppDataInit
{
    public static void DeleteDatabase(AppDbContext context)
    {
        context.Database.EnsureDeleted();
    }

    public static void MigrateDatabase(AppDbContext context)
    {
        context.Database.Migrate();
    }


    public static void SeedAppData(AppDbContext context)
    {
        // -- Contact types ------------------------------------------------
        var existingContactTypes = context.ContactTypes.ToList();
        foreach (var (en, et) in InitialData.ContactTypesWithEt)
        {
            if (!existingContactTypes.Any(ct => ct.ContactTypeName.ToString() == en))
            {
                var name = new LangStr(en, "en");
                name.SetTranslation(et, "et");
                context.ContactTypes.Add(new ContactType()
                {
                    ContactTypeName = name,
                    CreatedBy = "system"
                });
            }
        }

        // -- Airports -----------------------------------------------------
        foreach (var airportData in InitialData.Airports)
        {
            var existingAirport = context.Airports.FirstOrDefault(a => a.IcaoCode == airportData.IcaoCode);
            if (existingAirport == null)
            {
                var airportName = new LangStr(airportData.Name, "en");
                airportName.SetTranslation(airportData.NameEt, "et");

                var airportCity = new LangStr(airportData.City, "en");
                airportCity.SetTranslation(airportData.CityEt, "et");

                var airportCountry = new LangStr(airportData.Country, "en");
                airportCountry.SetTranslation(airportData.CountryEt, "et");

                context.Airports.Add(new Airport
                {
                    IcaoCode = airportData.IcaoCode,
                    IataCode = airportData.IataCode,
                    Name = airportName,
                    City = airportCity,
                    Country = airportCountry,
                    Latitude = airportData.Latitude,
                    Longitude = airportData.Longitude,
                    Elevation = airportData.Elevation,
                    CreatedBy = "system"
                });
            }
        }

        context.SaveChanges();

        // -- Companies ----------------------------------------------------
        foreach (var companyData in InitialData.Companies)
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

        // -- Aircraft -----------------------------------------------------
        // Build lookup dictionaries so we can resolve foreign keys by slug / ICAO
        var companyBySlug = context.Companies
            .ToDictionary(c => c.Slug, c => c.Id);

        var airportByIcao = context.Airports
            .ToDictionary(a => a.IcaoCode, a => a.Id);

        foreach (var acData in InitialData.Aircraft)
        {
            var existingAircraft = context.Aircraft
                .FirstOrDefault(a => a.RegistrationNumber == acData.Registration);

            if (existingAircraft == null)
            {
                if (!companyBySlug.TryGetValue(acData.CompanySlug, out var companyId))
                {
                    Console.WriteLine($"[Seed] Skipping aircraft {acData.Registration}: company '{acData.CompanySlug}' not found");
                    continue;
                }

                if (!airportByIcao.TryGetValue(acData.BaseAirportIcao, out var airportId))
                {
                    Console.WriteLine($"[Seed] Skipping aircraft {acData.Registration}: airport '{acData.BaseAirportIcao}' not found");
                    continue;
                }

                var category = new LangStr(acData.Category, "en");
                category.SetTranslation(acData.CategoryEt, "et");

                var description = new LangStr(acData.Description, "en");
                description.SetTranslation(acData.DescriptionEt, "et");

                context.Aircraft.Add(new Aircraft
                {
                    RegistrationNumber = acData.Registration,
                    Make = acData.Make,
                    Model = acData.Model,
                    Year = acData.Year,
                    Category = category,
                    RequiredLicenseType = acData.RequiredLicenseType,
                    TotalAirspeedHours = acData.TotalHours,
                    HourlyRate = acData.HourlyRate,
                    BaseAirportId = airportId,
                    CompanyId = companyId,
                    Description = description,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });

                Console.WriteLine($"[Seed] Added aircraft {acData.Registration} ({acData.Make} {acData.Model}) to {acData.CompanySlug}");
            }
        }

        context.SaveChanges();
    }
    
     public static void SeedIdentity(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
     {
         foreach (var roleName in InitialData.Roles)
         {
             var role = roleManager.FindByNameAsync(roleName).Result;

             if (role != null) continue;

             role = new AppRole()
             {
                 Name = roleName,
             };

             var result = roleManager.CreateAsync(role).Result;
             if (!result.Succeeded)
             {
                 throw new ApplicationException("Role creation failed!");
             }
         }


         foreach (var userInfo in InitialData.Users)
         {
             var user = userManager.FindByEmailAsync(userInfo.email).Result;
             if (user == null)
             {
                 user = new AppUser()
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

     public static void SeedAppUserCompanies(AppDbContext context)
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
              // Determine the user's Identity role from InitialData
              var userInfo = InitialData.Users.FirstOrDefault(u => u.email == user.Email);
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
