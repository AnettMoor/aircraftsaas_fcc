using Booking.Application.Interfaces;
using Booking.Domain.Enums;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.CompanyOwner;

namespace WebApp.Controllers;

/// <summary>
/// Company-owner dashboard – statistics and overview.
/// </summary>
[Authorize(Roles = "CompanyOwner,SystemAdmin")]
public class DashboardController : TenantAwareController
{
    private readonly IAircraftService _aircraftService;
    private readonly IBookingService _bookingService;
    private readonly ICompanyService _companyService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IAircraftService aircraftService,
        IBookingService bookingService,
        ICompanyService companyService,
        ITenantContext tenantContext,
        ILogger<DashboardController> logger)
        : base(tenantContext, companyService)
    {
        _aircraftService = aircraftService;
        _bookingService = bookingService;
        _companyService = companyService;
        _logger = logger;
    }

    /// <summary>
    /// Company owner dashboard with statistics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (await IsUserNormalRoleAsync(tenantId))
        {
            TempData["Error"] = "You do not have permission to access this page.";
            return RedirectToAction("Index", "Home");
        }

        var company = await _companyService.GetByIdAsync(tenantId.Value);
        var aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
        var bookings = await _bookingService.GetAllForCompanyAsync(tenantId.Value);

        var model = new DashboardViewModel
        {
            Company = company,
            TotalAircraft = aircraft.Count(),
            AvailableAircraft = aircraft.Count(a => a.IsAvailable),
            TotalBookings = bookings.Count(),
            PendingBookings = bookings.Count(b =>
                b.Status == EBookingStatus.Pending || b.Status == EBookingStatus.Requested),
            ActiveBookings = bookings.Count(b =>
                b.Status == EBookingStatus.Approved || b.Status == EBookingStatus.Paid),
            CompletedBookings = bookings.Count(b => b.Status == EBookingStatus.Completed),
            MonthlyRevenue = bookings
                .Where(b => b.Status == EBookingStatus.Completed &&
                            b.CompletedAt?.Month == DateTime.Now.Month)
                .Sum(b => b.TotalAmount),
            RecentAircraft = aircraft.OrderByDescending(a => a.Id).Take(5),
            PendingApprovalBookings = bookings
                .Where(b => b.Status == EBookingStatus.Pending ||
                            b.Status == EBookingStatus.Requested)
                .OrderByDescending(b => b.CreatedAt)
                .Take(5),
            RecentBookings = bookings.OrderByDescending(b => b.CreatedAt).Take(5),
            TotalUsers = 1 // Placeholder
        };

        return View(model);
    }
}
