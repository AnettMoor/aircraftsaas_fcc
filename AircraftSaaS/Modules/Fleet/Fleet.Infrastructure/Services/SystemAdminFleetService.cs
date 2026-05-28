using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Booking;
using Shared.Contracts.Common;
using Shared.Contracts.Users;
using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;

namespace Fleet.Infrastructure.Services;

internal sealed class SystemAdminFleetService : ISystemAdminFleetService
{
    private readonly FleetDbContext _db;
    private readonly IUsersModuleApi _usersApi;
    private readonly IBookingModuleApi _bookingApi;
    private readonly ILogger<SystemAdminFleetService> _logger;

    public SystemAdminFleetService(
        FleetDbContext db,
        IUsersModuleApi usersApi,
        IBookingModuleApi bookingApi,
        ILogger<SystemAdminFleetService> logger)
    {
        _db = db;
        _usersApi = usersApi;
        _bookingApi = bookingApi;
        _logger = logger;
    }

    // ── All Aircraft (system-wide) ───────────────────────────────────────────

    public async Task<AircraftListDto> GetAllAircraftAsync(string? search, Guid? tenantId, bool? available, int page, int pageSize)
    {
        var query = _db.Aircrafts
            .Include(a => a.BaseAirport)
            .Where(a => a.DeletedAt == null)
            .AsNoTracking()
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(a => a.CompanyId == tenantId.Value);

        if (available.HasValue)
            query = query.Where(a => a.IsAvailable == available.Value);

        var allAircraftEntities = await query
            .OrderBy(a => a.RegistrationNumber)
            .ToListAsync();

        // Cross-module: company names via MediatR (batch)
        var companyIds = allAircraftEntities.Select(a => a.CompanyId).Distinct().ToList();
        var companyNames = new Dictionary<Guid, string>();
        foreach (var companyId in companyIds)
        {
            var company = await _usersApi.GetCompanyByIdAsync(companyId);
            if (company != null)
                companyNames[companyId] = company.Name;
        }

        // Cross-module: booking counts per aircraft via MediatR (batch)
        var bookingCounts = new Dictionary<Guid, int>();
        foreach (var aircraft in allAircraftEntities)
        {
            var bookings = await _bookingApi.GetBookingsByAircraftAsync(aircraft.Id);
            bookingCounts[aircraft.Id] = bookings.Count;
        }

        var allAircraft = allAircraftEntities.Select(a => new SystemAdminAircraftDto
        {
            AircraftId = a.Id,
            RegistrationNumber = a.RegistrationNumber,
            Make = a.Make.ToString(),
            Model = a.Model.ToString(),
            Year = a.Year,
            HourlyRate = a.HourlyRate,
            IsAvailable = a.IsAvailable,
            CompanyName = companyNames.TryGetValue(a.CompanyId, out var cn) ? cn : "",
            BaseAirport = a.BaseAirport != null
                ? (a.BaseAirport.IcaoCode + " – " + a.BaseAirport.Name.ToString())
                : "",
            TotalBookings = bookingCounts.TryGetValue(a.Id, out var bc) ? bc : 0,
            CreatedAt = a.CreatedAt
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            allAircraft = allAircraft.Where(a =>
                a.RegistrationNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Make.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Model.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.CompanyName.Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = allAircraft.Count;
        var paged = allAircraft.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Cross-module: active companies for select dropdown
        var companies = await _usersApi.GetActiveCompaniesAsync();

        return new AircraftListDto
        {
            Aircraft = new PagedResult<SystemAdminAircraftDto>
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            Companies = companies
        };
    }

    // ── Airports ─────────────────────────────────────────────────────────────

    public async Task<AirportsListDto> GetAirportsAsync(string? search, bool showDeleted, int page, int pageSize)
    {
        var allAirports = await _db.Airports
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();

        var filtered = showDeleted
            ? allAirports
            : allAirports.Where(a => a.DeletedAt == null).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            filtered = filtered.Where(a =>
                a.IcaoCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.IataCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Name.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.City.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Country.ToString().Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = filtered.Count;
        var deletedCount = allAirports.Count(a => a.DeletedAt != null);

        var aircraftCountsRaw = await _db.Aircrafts
            .GroupBy(a => a.BaseAirportId)
            .Select(g => new { AirportId = g.Key, Count = g.Count() })
            .ToListAsync();
        var aircraftCounts = aircraftCountsRaw.ToDictionary(x => x.AirportId, x => x.Count);

        var paged = filtered
            .OrderBy(a => a.IcaoCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new SystemAdminAirportDto
            {
                AirportId = a.Id,
                IcaoCode = a.IcaoCode,
                IataCode = a.IataCode,
                Name = a.Name.ToString(),
                City = a.City.ToString(),
                Country = a.Country.ToString(),
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                Elevation = a.Elevation,
                AircraftCount = aircraftCounts.TryGetValue(a.Id, out var cnt) ? cnt : 0,
                IsDeleted = a.DeletedAt.HasValue,
                DeletedBy = a.DeletedBy,
                DeletedAt = a.DeletedAt
            })
            .ToList();

        return new AirportsListDto
        {
            Airports = new PagedResult<SystemAdminAirportDto>
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            DeletedAirports = deletedCount
        };
    }

    public async Task<AirportEditDto?> GetAirportForEditAsync(Guid id)
    {
        var airport = await _db.Airports
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);

        if (airport == null) return null;

        return new AirportEditDto
        {
            Id = airport.Id,
            IcaoCode = airport.IcaoCode,
            IataCode = airport.IataCode,
            Name = airport.Name.ToString(),
            City = airport.City.ToString(),
            Country = airport.Country.ToString(),
            Latitude = airport.Latitude,
            Longitude = airport.Longitude,
            Elevation = airport.Elevation
        };
    }

    public async Task<bool> AirportExistsByIcaoCodeAsync(string icaoCode, Guid? excludeId = null)
    {
        var query = _db.Airports.IgnoreQueryFilters()
            .Where(a => a.IcaoCode == icaoCode.ToUpper());

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> HasActiveAircraftAtAirportAsync(Guid airportId)
    {
        return await _db.Aircrafts.AnyAsync(a => a.BaseAirportId == airportId && a.DeletedAt == null);
    }
}
