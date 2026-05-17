using System.Security.Claims;
using App.Application.Interfaces;
using App.Domain.Enums;
using App.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.User;

namespace WebApp.Controllers;

/// <summary>
/// User profile management.
/// </summary>
[Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
public class ProfileController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IBookingService bookingService,
        UserManager<AppUser> userManager,
        ILogger<ProfileController> logger)
    {
        _bookingService = bookingService;
        _userManager = userManager;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value is string s
            && Guid.TryParse(s, out var id)
            ? id
            : null;
    }

    /// <summary>
    /// View user profile.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null) return NotFound();

        var bookings = await _bookingService.GetAllForPilotAsync(userId.Value);

        var model = new ProfileViewModel
        {
            UserId = user.Id.ToString(),
            Name = $"{user.FirstName} {user.LastName}".Trim().Length > 0
                ? $"{user.FirstName} {user.LastName}".Trim()
                : user.UserName ?? "Unknown",
            Email = user.Email ?? "",
            Phone = user.PhoneNumber,
            TotalBookings = bookings.Count(),
            CompletedBookings = bookings.Count(b => b.Status == EBookingStatus.Completed)
        };

        return View(model);
    }
}
