using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Booking.Application.DTOs;
using Booking.Application.Interfaces;
using Booking.Domain.Enums;
using Shared.Contracts.Common;
using Shared.Contracts.Fleet;
using Shared.Contracts.Users;

namespace Booking.Infrastructure.Services;

internal sealed class SystemAdminBookingService : ISystemAdminBookingService
{
    private readonly BookingDbContext _db;
    private readonly IFleetModuleApi _fleetApi;
    private readonly IUsersModuleApi _usersApi;
    private readonly ILogger<SystemAdminBookingService> _logger;

    public SystemAdminBookingService(
        BookingDbContext db,
        IFleetModuleApi fleetApi,
        IUsersModuleApi usersApi,
        ILogger<SystemAdminBookingService> logger)
    {
        _db = db;
        _fleetApi = fleetApi;
        _usersApi = usersApi;
        _logger = logger;
    }

    // ── All Bookings (system-wide) ───────────────────────────────────────────

    public async Task<BookingsListDto> GetAllBookingsAsync(string? search, string? status, Guid? tenantId, int page, int pageSize)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(b => b.CompanyId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<EBookingStatus>(status, out var parsedStatus))
        {
            query = query.Where(b => b.Status == parsedStatus);
        }

        var bookingEntities = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Cross-module: batch-fetch company names (single call instead of N)
        var companyIds = bookingEntities.Select(b => b.CompanyId).Distinct();
        var companyMap = await _usersApi.GetCompaniesByIdsAsync(companyIds);
        var companyNames = companyMap.ToDictionary(c => c.Key, c => c.Value.Name);

        // Cross-module: batch-fetch aircraft registrations (single call instead of N)
        var aircraftIds = bookingEntities.Select(b => b.AircraftId).Distinct();
        var aircraftMap = await _fleetApi.GetAircraftsByIdsAsync(aircraftIds);
        var aircraftRegs = aircraftMap.ToDictionary(a => a.Key, a => a.Value.Registration);

        // Cross-module: batch-fetch pilot emails (single call instead of N)
        var pilotIds = bookingEntities.Select(b => b.PilotId).Distinct();
        var pilotMap = await _usersApi.GetUsersByIdsAsync(pilotIds);
        var pilotEmails = pilotMap.ToDictionary(u => u.Key, u => u.Value.Email);

        var allBookings = bookingEntities.Select(b => new SystemAdminBookingDto
        {
            BookingId = b.Id,
            CompanyName = companyNames.TryGetValue(b.CompanyId, out var cn) ? cn : "",
            AircraftRegistration = aircraftRegs.TryGetValue(b.AircraftId, out var ar) ? ar : "",
            PilotEmail = pilotEmails.TryGetValue(b.PilotId, out var pe) ? pe : "",
            StartDateTime = b.StartDateTime,
            EndDateTime = b.EndDateTime,
            Status = b.Status,
            TotalAmount = b.TotalAmount,
            CreatedAt = b.CreatedAt
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            allBookings = allBookings.Where(b =>
                b.PilotEmail.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                b.AircraftRegistration.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                b.CompanyName.Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = allBookings.Count;
        var paged = allBookings.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Cross-module: active companies for select dropdown
        var companies = await _usersApi.GetActiveCompaniesAsync();

        return new BookingsListDto
        {
            Bookings = new PagedResult<SystemAdminBookingDto>
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            Companies = companies
        };
    }
}
