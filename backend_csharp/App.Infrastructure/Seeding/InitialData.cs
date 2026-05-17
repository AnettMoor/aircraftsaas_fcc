namespace App.Infrastructure.Seeding;

public static class InitialData
{
    public static readonly string[] ContactTypes = [
        "email",
        "post",
        "phone"
    ];

    public static readonly (string En, string Et)[] ContactTypesWithEt = [
        ("email", "e-post"),
        ("post", "post"),
        ("phone", "telefon")
    ];

    public static readonly AirportSeedData[] Airports = [
        new("TLLA", "TLL", "Tallinn Airport",           "Tallinna lennujaam",          "Tallinn",  "Tallinn",   "Estonia",  "Eesti",    59.4133, 24.8328, 131),
        new("EFHK", "HEL", "Helsinki-Vantaa Airport",   "Helsingi-Vantaa lennujaam",   "Helsinki", "Helsingi",  "Finland",  "Soome",    60.3172, 24.9633, 167),
        new("EVRA", "RIX", "Riga International Airport", "Riia rahvusvaheline lennujaam", "Riga",   "Riia",      "Latvia",   "L�ti",     56.9236, 23.9711, 36)
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

    // -- Companies --------------------------------------------------------
    public static readonly CompanySeedData[] Companies = [
        new(
            Name: "Baltic Air Charter",
            NameEt: "Baltic Air Charter",
            Slug: "baltic-air-charter",
            MaxUsers: 10,
            MaxAircraft: 20,
            MaxBookingsPerMonth: 100,
            Address: "Lennujaama tee 12, Tallinn 11101, Estonia",
            Phone: "+372 605 8888",
            Email: "info@balticaircharter.ee"
        ),
        new(
            Name: "Nordic Wings O�",
            NameEt: "Nordic Wings O�",
            Slug: "nordic-wings",
            MaxUsers: 999,
            MaxAircraft: 999,
            MaxBookingsPerMonth: 9999,
            Address: "Lent�j�ntie 3, 01530 Vantaa, Finland",
            Phone: "+358 9 123 4567",
            Email: "ops@nordicwings.fi"
        ),
        new(
            Name: "Riga Flight Services",
            NameEt: "Riia lennusteenused",
            Slug: "riga-flight-services",
            MaxUsers: 2,
            MaxAircraft: 3,
            MaxBookingsPerMonth: 20,
            Address: "Lidosta Riga 10/1, Marupes novads, LV-1053, Latvia",
            Phone: "+371 6720 7009",
            Email: "contact@rigaflight.lv"
        )
    ];

    public record CompanySeedData(
        string Name,
        string NameEt,
        string Slug,
        int MaxUsers,
        int MaxAircraft,
        int MaxBookingsPerMonth,
        string Address,
        string Phone,
        string Email
    );

    // -- Aircraft ---------------------------------------------------------
    public static readonly AircraftSeedData[] Aircraft = [
        // Baltic Air Charter � 2 aircraft based at Tallinn (TLLA)
        new(
            Registration: "ES-TCA",
            Make: "Cessna",
            Model: "172 Skyhawk",
            Year: 2018,
            Category: "SingleEngine",
            CategoryEt: "�heooteriline",
            RequiredLicenseType: "PPL",
            TotalHours: 1200,
            HourlyRate: 150m,
            BaseAirportIcao: "TLLA",
            CompanySlug: "baltic-air-charter",
            Description: "Well-maintained Cessna 172 ideal for training and short sightseeing flights over the Baltic coast.",
            DescriptionEt: "H�stihooldatud Cessna 172, ideaalne treeninglendudeks ja l�hikesteks vaatluslendu�deks �le L��nemere ranniku."
        ),
        new(
            Registration: "ES-PPA",
            Make: "Piper",
            Model: "PA-28 Cherokee",
            Year: 2015,
            Category: "SingleEngine",
            CategoryEt: "�heooteriline",
            RequiredLicenseType: "PPL",
            TotalHours: 2400,
            HourlyRate: 120m,
            BaseAirportIcao: "TLLA",
            CompanySlug: "baltic-air-charter",
            Description: "Reliable Piper Cherokee with great cross-country range, perfect for island-hopping in Estonia.",
            DescriptionEt: "Usaldusv��rne Piper Cherokee suurep�rase ristl�ike lennuulatusega � ideaalne saarte vahel lennamiseks Eestis."
        ),
        // Nordic Wings O� � 3 aircraft based at Helsinki (EFHK)
        new(
            Registration: "OH-HEL",
            Make: "Diamond",
            Model: "DA42 Twin Star",
            Year: 2020,
            Category: "MultiEngine",
            CategoryEt: "Mitmeooteriline",
            RequiredLicenseType: "CPL",
            TotalHours: 800,
            HourlyRate: 280m,
            BaseAirportIcao: "EFHK",
            CompanySlug: "nordic-wings",
            Description: "Modern twin-engine Diamond DA42 with Garmin G1000 avionics, suitable for IFR training and charter.",
            DescriptionEt: "Kaasaegne kaheooteriline Diamond DA42 Garmin G1000 avioonikaraamatuga, sobib IFR-treeninguks ja t�arteriks."
        ),
        new(
            Registration: "OH-CIR",
            Make: "Cirrus",
            Model: "SR22",
            Year: 2022,
            Category: "SingleEngine",
            CategoryEt: "�heooteriline",
            RequiredLicenseType: "PPL",
            TotalHours: 350,
            HourlyRate: 220m,
            BaseAirportIcao: "EFHK",
            CompanySlug: "nordic-wings",
            Description: "Top-of-the-line Cirrus SR22 with CAPS parachute system and full glass cockpit.",
            DescriptionEt: "Tippklassi Cirrus SR22 CAPS-langevarju�s�steemi ja t�ieliku klaaskabiinega."
        ),
        new(
            Registration: "OH-ROB",
            Make: "Robinson",
            Model: "R44 Raven II",
            Year: 2019,
            Category: "Helicopter",
            CategoryEt: "Helikopter",
            RequiredLicenseType: "CPL",
            TotalHours: 1500,
            HourlyRate: 450m,
            BaseAirportIcao: "EFHK",
            CompanySlug: "nordic-wings",
            Description: "Versatile Robinson R44 helicopter for scenic tours, photography flights, and short transfers.",
            DescriptionEt: "Mitmek�lgne Robinson R44 helikopter vaatlusreis�ideks, fotolendudeks ja l�hikesteks �leviimisteks."
        ),
        // Riga Flight Services � 1 aircraft based at Riga (EVRA)
        new(
            Registration: "YL-RFS",
            Make: "Cessna",
            Model: "182 Skylane",
            Year: 2016,
            Category: "SingleEngine",
            CategoryEt: "�heooteriline",
            RequiredLicenseType: "PPL",
            TotalHours: 1800,
            HourlyRate: 170m,
            BaseAirportIcao: "EVRA",
            CompanySlug: "riga-flight-services",
            Description: "Dependable Cessna 182 Skylane, great for longer flights across the Baltics.",
            DescriptionEt: "Usaldusv��rne Cessna 182 Skylane, suurep�rane pikemateks lendudeks �le Baltikumi."
        )
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

    public static readonly string[] Roles = [
        "Normal",
        "CompanyOwner",
        "SystemAdmin"
    ];

    public static readonly (string email, string password, string[] roles)[] Users = [
        ("1@3", "3", ["Normal"]),
        ("1@2", "2", ["CompanyOwner"]),
         ("1@4", "4", ["SystemAdmin"])
        
    ];
    
}
