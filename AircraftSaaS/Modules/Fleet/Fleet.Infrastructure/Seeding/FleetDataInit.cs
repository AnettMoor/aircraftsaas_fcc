using Fleet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Domain;

namespace Fleet.Infrastructure.Seeding;

internal static class FleetDataInit
{
    public static void MigrateDatabase(FleetDbContext context)
    {
        context.Database.Migrate();
    }

    /// <summary>
    /// Seeds Fleet-owned data: airports and aircraft.
    /// Aircraft require cross-module CompanyIds, passed as a slug→Guid dictionary.
    /// Call this after the Users module has seeded companies.
    /// </summary>
    public static void SeedFleetData(FleetDbContext context, Dictionary<string, Guid> companyBySlug)
    {
        SeedAirports(context);
        SeedAircraft(context, companyBySlug);
    }

    /// <summary>
    /// Seeds airports (Fleet-owned, no cross-module dependencies).
    /// </summary>
    public static void SeedAirports(FleetDbContext context)
    {
        foreach (var airportData in InitialFleetData.Airports)
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
    }

    /// <summary>
    /// Seeds aircraft (requires cross-module CompanyId mapping and same-module AirportId).
    /// </summary>
    public static void SeedAircraft(FleetDbContext context, Dictionary<string, Guid> companyBySlug)
    {
        var airportByIcao = context.Airports
            .ToDictionary(a => a.IcaoCode, a => a.Id);

        foreach (var acData in InitialFleetData.Aircraft)
        {
            var existingAircraft = context.Aircrafts
                .FirstOrDefault(a => a.RegistrationNumber == acData.Registration);

            if (existingAircraft == null)
            {
                if (!companyBySlug.TryGetValue(acData.CompanySlug, out var companyId))
                {
                    Console.WriteLine($"[Fleet Seed] Skipping aircraft {acData.Registration}: company '{acData.CompanySlug}' not found");
                    continue;
                }

                if (!airportByIcao.TryGetValue(acData.BaseAirportIcao, out var airportId))
                {
                    Console.WriteLine($"[Fleet Seed] Skipping aircraft {acData.Registration}: airport '{acData.BaseAirportIcao}' not found");
                    continue;
                }

                var category = new LangStr(acData.Category, "en");
                category.SetTranslation(acData.CategoryEt, "et");

                var description = new LangStr(acData.Description, "en");
                description.SetTranslation(acData.DescriptionEt, "et");

                context.Aircrafts.Add(new Aircraft
                {
                    RegistrationNumber = acData.Registration,
                    Make = new LangStr(acData.Make),
                    Model = new LangStr(acData.Model),
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

                Console.WriteLine($"[Fleet Seed] Added aircraft {acData.Registration} ({acData.Make} {acData.Model}) to {acData.CompanySlug}");
            }
        }

        context.SaveChanges();
    }
}

/// <summary>
/// Static seed data for the Fleet module.
/// </summary>
internal static class InitialFleetData
{
    public static readonly AirportSeedData[] Airports =
    [
        new("TLLA", "TLL", "Tallinn Airport", "Tallinna lennujaam", "Tallinn", "Tallinn", "Estonia", "Eesti", 59.4133, 24.8328, 131),
        new("EFHK", "HEL", "Helsinki-Vantaa Airport", "Helsingi-Vantaa lennujaam", "Helsinki", "Helsingi", "Finland", "Soome", 60.3172, 24.9633, 167),
        new("EVRA", "RIX", "Riga International Airport", "Riia rahvusvaheline lennujaam", "Riga", "Riia", "Latvia", "Läti", 56.9236, 23.9711, 36)
    ];

    public record AirportSeedData(
        string IcaoCode,
        string IataCode,
        string Name,
        string NameEt,
        string City,
        string CityEt,
        string Country,
        string CountryEt,
        double Latitude,
        double Longitude,
        int Elevation
    );

    public static readonly AircraftSeedData[] Aircraft =
    [
        // Baltic Air Charter — 2 aircraft based at Tallinn (TLLA)
        new("ES-TCA", "Cessna", "172 Skyhawk", 2018, "SingleEngine", "Üheooteriline", "PPL", 1200, 150m, "TLLA", "baltic-air-charter",
            "Well-maintained Cessna 172 ideal for training and short sightseeing flights over the Baltic coast.",
            "Hästihooldatud Cessna 172, ideaalne treeninglendudeks ja lühikesteks vaatluslendudeks üle Läänemere ranniku."),
        new("ES-PPA", "Piper", "PA-28 Cherokee", 2015, "SingleEngine", "Üheooteriline", "PPL", 2400, 120m, "TLLA", "baltic-air-charter",
            "Reliable Piper Cherokee with great cross-country range, perfect for island-hopping in Estonia.",
            "Usaldusväärne Piper Cherokee suurepärase ristlõike lennuulatusega – ideaalne saarte vahel lennamiseks Eestis."),
        // Nordic Wings OÜ — 3 aircraft based at Helsinki (EFHK)
        new("OH-HEL", "Diamond", "DA42 Twin Star", 2020, "MultiEngine", "Mitmeooteriline", "CPL", 800, 280m, "EFHK", "nordic-wings",
            "Modern twin-engine Diamond DA42 with Garmin G1000 avionics, suitable for IFR training and charter.",
            "Kaasaegne kaheooteriline Diamond DA42 Garmin G1000 avioonikaraamatuga, sobib IFR-treeninguks ja tšarteriks."),
        new("OH-CIR", "Cirrus", "SR22", 2022, "SingleEngine", "Üheooteriline", "PPL", 350, 220m, "EFHK", "nordic-wings",
            "Top-of-the-line Cirrus SR22 with CAPS parachute system and full glass cockpit.",
            "Tippklassi Cirrus SR22 CAPS-langevarjusüsteemi ja täieliku klaaskabiinega."),
        new("OH-ROB", "Robinson", "R44 Raven II", 2019, "Helicopter", "Helikopter", "CPL", 1500, 450m, "EFHK", "nordic-wings",
            "Versatile Robinson R44 helicopter for scenic tours, photography flights, and short transfers.",
            "Mitmekülgne Robinson R44 helikopter vaatlusreisideks, fotolendudeks ja lühikesteks üleviimisteks."),
        // Riga Flight Services — 1 aircraft based at Riga (EVRA)
        new("YL-RFS", "Cessna", "182 Skylane", 2016, "SingleEngine", "Üheooteriline", "PPL", 1800, 170m, "EVRA", "riga-flight-services",
            "Dependable Cessna 182 Skylane, great for longer flights across the Baltics.",
            "Usaldusväärne Cessna 182 Skylane, suurepärane pikemateks lendudeks üle Baltikumi.")
    ];

    public record AircraftSeedData(
        string Registration,
        string Make,
        string Model,
        int Year,
        string Category,
        string CategoryEt,
        string RequiredLicenseType,
        int TotalHours,
        decimal HourlyRate,
        string BaseAirportIcao,
        string CompanySlug,
        string Description,
        string DescriptionEt
    );
}
