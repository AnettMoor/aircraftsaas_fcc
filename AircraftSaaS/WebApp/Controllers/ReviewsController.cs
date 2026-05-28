using Booking.Application.DTOs;
using Booking.Application.Interfaces;
using Fleet.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApp.ViewModels.Reviews;

namespace WebApp.Controllers;

[Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
// Read endpoints use [AllowAnonymous]; authoring reviews restricted to Normal,CompanyOwner per-action.
public class ReviewsController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly IAircraftService _aircraftService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        IReviewService reviewService,
        IAircraftService aircraftService,
        ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _aircraftService = aircraftService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } userIdStr 
            && Guid.TryParse(userIdStr, out var userId) 
            ? userId 
            : null;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var reviews = await _reviewService.GetAllReviewsAsync();
        var model = new ReviewIndexViewModel
        {
            Reviews = reviews
        };
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ByAircraft(Guid aircraftId)
    {
        var reviews = await _reviewService.GetReviewsByAircraftIdAsync(aircraftId);
        var aircraft = await _aircraftService.GetByIdAsync(aircraftId, null);
        
        var model = new ReviewByAircraftViewModel
        {
            Reviews = reviews,
            Aircraft = aircraft
        };
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid id)
    {
        var review = await _reviewService.GetReviewByIdAsync(id);
        if (review == null)
            return NotFound();
        
        var model = new ReviewDetailsViewModel
        {
            Review = review
        };
        return View(model);
    }

    /// <summary>
    /// Create review form – GET. (Normal, CompanyOwner only — SystemAdmin cannot author reviews)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Create(Guid aircraftId, Guid? bookingId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var aircraft = await _aircraftService.GetByIdAsync(aircraftId, null);
        if (aircraft == null)
            return NotFound();

        var model = new ReviewCreateViewModel
        {
            AircraftId = aircraftId,
            BookingId = bookingId ?? Guid.Empty,
            Rating = 5,
            Aircraft = aircraft
        };

        return View(model);
    }

    /// <summary>
    /// Create review – POST. (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> Create(ReviewCreateViewModel model)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        if (!ModelState.IsValid)
        {
            model.Aircraft = await _aircraftService.GetByIdAsync(model.AircraftId, null);
            return View(model);
        }

        var dto = new CreateReviewDto
        {
            AircraftId = model.AircraftId,
            BookingId = model.BookingId,
            Rating = model.Rating,
            Comment = model.Comment
        };

        try
        {
            var review = await _reviewService.CreateReviewAsync(dto, userId.Value);
            TempData["Success"] = "Review submitted successfully!";
            return RedirectToAction(nameof(Details), new { id = review.Id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error creating review");
            ModelState.AddModelError("", ex.Message);
            model.Aircraft = await _aircraftService.GetByIdAsync(model.AircraftId, null);
            return View(model);
        }
    }

    /// <summary>
    /// Edit review – GET. (Author or SystemAdmin for moderation)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var review = await _reviewService.GetReviewByIdAsync(id);
        if (review == null)
            return NotFound();

        // Only the author or admin can edit
        var isAdmin = User.IsInRole("SystemAdmin");
        if (review.AuthorId != userId.Value && !isAdmin)
        {
            TempData["Error"] = "You are not authorized to edit this review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new ReviewEditViewModel
        {
            Id = id,
            Rating = review.Rating,
            Comment = review.Comment,
            Review = review
        };

        return View(model);
    }

    /// <summary>
    /// Edit review – POST. (Author or SystemAdmin for moderation)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
    public async Task<IActionResult> Edit(Guid id, ReviewEditViewModel model)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.Review = await _reviewService.GetReviewByIdAsync(id);
            return View(model);
        }

        var dto = new UpdateReviewDto
        {
            Rating = model.Rating,
            Comment = model.Comment
        };

        try
        {
            await _reviewService.UpdateReviewAsync(id, dto, userId.Value, isAdmin);
            TempData["Success"] = "Review updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to edit this review.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Delete review – POST. (Author or SystemAdmin for moderation)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");

        try
        {
            await _reviewService.DeleteReviewAsync(id, userId.Value, isAdmin);
            TempData["Success"] = "Review deleted successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to delete this review.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// My Reviews – reviews authored by the current user. (Normal, CompanyOwner only — SystemAdmin has no personal reviews)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Normal,CompanyOwner")]
    public async Task<IActionResult> MyReviews()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var allReviews = await _reviewService.GetAllReviewsAsync();
        var userReviews = allReviews.Where(r => r.AuthorId == userId.Value).ToList();

        var model = new ReviewIndexViewModel
        {
            Reviews = userReviews
        };
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> AverageRating(Guid aircraftId)
    {
        var rating = await _reviewService.GetAverageRatingForAircraftAsync(aircraftId);
        return Json(new { aircraftId, rating });
    }
}
