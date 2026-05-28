using Booking.Application.DTOs;
using Booking.Application.Interfaces;
using Booking.Domain.Enums;
using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.CompanyOwner;
using WebApp.ViewModels.User;

namespace WebApp.Controllers;

/// <summary>
/// Unified Bookings controller – pilot booking flow AND company-owner
/// booking management in a single feature-based controller.
/// </summary>
[Authorize(Roles = "Normal,CompanyOwner")]
public class BookingsController : TenantAwareController
{
    private readonly IBookingService _bookingService;
    private readonly IAircraftService _aircraftService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        IBookingService bookingService,
        IAircraftService aircraftService,
        ITenantContext tenantContext,
        ICompanyService companyService,
        ILogger<BookingsController> logger)
        : base(tenantContext, companyService)
    {
        _bookingService = bookingService;
        _aircraftService = aircraftService;
        _logger = logger;
    }

    // ── Pilot Booking Flow ───────────────────────────────────────────

    /// <summary>
    /// Pilot's own bookings list.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var bookings = await _bookingService.GetAllForPilotAsync(userId.Value);

        var model = new MyBookingsViewModel
        {
            ActiveBookings = bookings.Where(b =>
                b.Status == EBookingStatus.Approved || b.Status == EBookingStatus.Paid),
            PendingBookings = bookings.Where(b =>
                b.Status == EBookingStatus.Pending || b.Status == EBookingStatus.Requested),
            PastBookings = bookings.Where(b => b.Status == EBookingStatus.Completed),
            CancelledBookings = bookings.Where(b =>
                b.Status == EBookingStatus.Cancelled || b.Status == EBookingStatus.Rejected)
        };

        return View(model);
    }

    /// <summary>
    /// Booking details – works for both pilot (own) and company-owner context.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = TenantContext.GetCurrentTenantId();
        if (tenantId.HasValue)
        {
            var isMember = await TenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value);
            if (!isMember) tenantId = null;
        }

        var booking = await _bookingService.GetByIdAsync(id, tenantId, tenantId.HasValue ? null : userId);
        if (booking == null) return NotFound();

        // Determine capabilities based on context
        var isCompanyContext = tenantId.HasValue;
        var canCancel = booking.Status == EBookingStatus.Pending ||
                        booking.Status == EBookingStatus.Requested;
        var canEdit = booking.Status == EBookingStatus.Pending ||
                      booking.Status == EBookingStatus.Requested;
        var canPay = booking.Status == EBookingStatus.Approved;

        var model = new BookingDetailsViewModel
        {
            Booking = booking,
            CanCancel = canCancel,
            CanEdit = canEdit,
            CanPay = canPay,
            CanReview = false // Set below if applicable
        };

        // Check review eligibility only for pilot's own bookings
        if (!isCompanyContext || booking.PilotId == userId.Value)
        {
            var existingReview = await GetReviewService()?.GetReviewByBookingIdAsync(id)!;
            model.CanReview = booking.Status == EBookingStatus.Completed && existingReview == null;
            model.ExistingReview = existingReview;
        }

        return View(model);
    }

    // Lazy resolve – not injected via ctor because it's only needed for review checks
    private IReviewService? GetReviewService()
        => HttpContext.RequestServices.GetService<IReviewService>();

    /// <summary>
    /// Create booking (book an aircraft) – GET form. (Normal, CompanyOwner only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Create(Guid? aircraftId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "Identity" });

        AircraftDto? aircraft = null;
        if (aircraftId.HasValue)
            aircraft = await _aircraftService.GetByIdAsync(aircraftId.Value, null);

        var model = new BookAircraftViewModel
        {
            Aircraft = aircraft!,
            BookingModel = new CreateBookingDto
            {
                AircraftId = aircraftId ?? Guid.Empty,
                StartDateTime = DateTime.Now.Date.AddHours(9),
                EndDateTime = DateTime.Now.Date.AddHours(17)
            },
            IsAvailable = true,
            AvailableFrom = DateTime.Now
        };

        return View(model);
    }

    /// <summary>
    /// Create booking – POST. (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Create(CreateBookingDto bookingModel)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var aircraftId = bookingModel.AircraftId;

        async Task<IActionResult> ReturnViewWithModel(bool isAvailable = true)
        {
            var aircraft = await _aircraftService.GetByIdAsync(aircraftId, null);
            var vm = new BookAircraftViewModel
            {
                Aircraft = aircraft!,
                BookingModel = bookingModel,
                IsAvailable = isAvailable,
                AvailableFrom = isAvailable ? DateTime.Now : null
            };
            return View(vm);
        }

        if (!ModelState.IsValid)
            return await ReturnViewWithModel();

        var isValid = await _bookingService.ValidateBookingAsync(
            aircraftId, bookingModel.StartDateTime, bookingModel.EndDateTime);
        if (!isValid)
        {
            ModelState.AddModelError("", "Aircraft is not available for the selected dates.");
            return await ReturnViewWithModel(false);
        }

        try
        {
            await _bookingService.RequestBookingAsync(bookingModel, userId.Value);
            TempData["Success"] = "Booking request submitted successfully!";
            return RedirectToAction(nameof(MyBookings));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await ReturnViewWithModel();
        }
    }

    /// <summary>
    /// Edit booking – GET form (pilot, pending only). (Normal, CompanyOwner only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var booking = await _bookingService.GetByIdAsync(id, null, userId.Value);
        if (booking == null) return NotFound();

        if (booking.Status != EBookingStatus.Pending && booking.Status != EBookingStatus.Requested)
        {
            TempData["Error"] = "Only pending or requested bookings can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var aircraft = await _aircraftService.GetByIdAsync(booking.AircraftId, null);

        var model = new EditBookingViewModel
        {
            Booking = booking,
            EditModel = new UpdateBookingDto
            {
                Id = booking.Id,
                StartDateTime = booking.StartDateTime,
                EndDateTime = booking.EndDateTime,
                Purpose = booking.Purpose
            },
            HourlyRate = aircraft?.HourlyRate ?? 0
        };

        return View(model);
    }

    /// <summary>
    /// Edit booking – POST. (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Edit(UpdateBookingDto editModel)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (!ModelState.IsValid)
        {
            var booking = await _bookingService.GetByIdAsync(editModel.Id, null, userId.Value);
            if (booking == null) return NotFound();
            var aircraft = await _aircraftService.GetByIdAsync(booking.AircraftId, null);
            return View(new EditBookingViewModel
            {
                Booking = booking,
                EditModel = editModel,
                HourlyRate = aircraft?.HourlyRate ?? 0
            });
        }

        try
        {
            await _bookingService.UpdateBookingAsync(editModel, userId.Value);
            TempData["Success"] = "Booking updated successfully!";
            return RedirectToAction(nameof(Details), new { id = editModel.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            var booking = await _bookingService.GetByIdAsync(editModel.Id, null, userId.Value);
            if (booking == null) return NotFound();
            var aircraft = await _aircraftService.GetByIdAsync(booking.AircraftId, null);
            return View(new EditBookingViewModel
            {
                Booking = booking,
                EditModel = editModel,
                HourlyRate = aircraft?.HourlyRate ?? 0
            });
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to edit this booking.";
            return RedirectToAction(nameof(Details), new { id = editModel.Id });
        }
    }

    /// <summary>
    /// Cancel booking – POST (pilot or company owner). (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Cancel(Guid id, string? returnTo)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        try
        {
            await _bookingService.CancelAsync(id, userId.Value);
            TempData["Success"] = "Booking cancelled successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to cancel this booking.";
        }

        if (string.Equals(returnTo, "CompanyBookings", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(CompanyBookings));
        }

        return RedirectToAction(nameof(MyBookings));
    }

    /// <summary>
    /// Pay for booking – POST (pilot). (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Pay(Guid id, string paymentMethod)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var paymentDto = new PaymentDto
        {
            PaymentMethod = paymentMethod,
            TransactionId = Guid.NewGuid().ToString()
        };

        try
        {
            await _bookingService.ConfirmPaymentAsync(id, paymentDto, userId.Value);
            TempData["Success"] = "Payment successful!";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to pay for this booking.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Validate booking dates (JSON endpoint).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Validate(
        [FromQuery] Guid aircraftId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        var isValid = await _bookingService.ValidateBookingAsync(aircraftId, start, end);
        return Json(new { isValid });
    }

    // ── Company Owner Booking Management ─────────────────────────────

    /// <summary>
    /// Company booking list for management. (CompanyOwner only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> CompanyBookings()
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (await IsUserNormalRoleAsync(tenantId))
        {
            TempData["Error"] = "You do not have permission to manage bookings.";
            return RedirectToAction("Index", "Home");
        }

        var bookings = await _bookingService.GetAllForCompanyAsync(tenantId.Value);

        var model = new BookingManagementViewModel
        {
            AllBookings = bookings,
            PendingBookings = bookings.Where(b =>
                b.Status == EBookingStatus.Pending || b.Status == EBookingStatus.Requested),
            ApprovedBookings = bookings.Where(b => b.Status == EBookingStatus.Approved),
            PaidBookings = bookings.Where(b => b.Status == EBookingStatus.Paid),
            CompletedBookings = bookings.Where(b => b.Status == EBookingStatus.Completed),
            CancelledBookings = bookings.Where(b =>
                b.Status == EBookingStatus.Cancelled || b.Status == EBookingStatus.Rejected)
        };

        return View(model);
    }

    /// <summary>
    /// Approve booking – POST (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        try
        {
            await _bookingService.ApproveAsync(id, tenantId.Value);
            TempData["Success"] = "Booking approved successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CompanyBookings));
    }

    /// <summary>
    /// Reject booking – POST (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Reject(Guid id, string reason)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        try
        {
            await _bookingService.RejectAsync(id, tenantId.Value, reason);
            TempData["Success"] = "Booking rejected.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CompanyBookings));
    }

    /// <summary>
    /// Complete booking – POST (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        try
        {
            await _bookingService.CompleteAsync(id, tenantId.Value);
            TempData["Success"] = "Booking marked as completed.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CompanyBookings));
    }
}
