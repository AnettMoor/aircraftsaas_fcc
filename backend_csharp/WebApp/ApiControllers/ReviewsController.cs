using System.Net;
using System.Security.Claims;
using App.Application.Interfaces;
using WebApp.v1;
using WebApp.v1.Mappers;
using Asp.Versioning;
using Base.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.ApiControllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
// Read endpoints are [AllowAnonymous]; write/mutate endpoints have per-action role restrictions below.
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        IReviewService reviewService,
        ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Get all reviews (public).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<ReviewResponse>>> GetReviews()
    {
        var reviews = await _reviewService.GetAllReviewsAsync();
        return Ok(reviews.ToResponse());
    }

    /// <summary>
    /// Get a single review by ID (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<ReviewResponse>> GetReview(Guid id)
    {
        var review = await _reviewService.GetReviewByIdAsync(id);
        if (review == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Review with id {id} not found."
            });

        return Ok(review.ToResponse());
    }

    /// <summary>
    /// Get all reviews for a specific aircraft (public).
    /// </summary>
    [HttpGet("aircraft/{aircraftId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<ReviewResponse>>> GetReviewsByAircraft(Guid aircraftId)
    {
        var reviews = await _reviewService.GetReviewsByAircraftIdAsync(aircraftId);
        return Ok(reviews.ToResponse());
    }

    /// <summary>
    /// Get the average rating for a specific aircraft (public).
    /// </summary>
    [HttpGet("aircraft/{aircraftId:guid}/rating")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<double>> GetAircraftRating(Guid aircraftId)
    {
        var rating = await _reviewService.GetAverageRatingForAircraftAsync(aircraftId);
        return Ok(rating);
    }

    /// <summary>
    /// Create a review (Normal, CompanyOwner only — must have a completed booking. SystemAdmin cannot author reviews).
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<ReviewResponse>> PostReview([FromBody] CreateReviewRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        try
        {
            var review = await _reviewService.CreateReviewAsync(request.ToBllDto(), userId.Value);
            return CreatedAtAction(nameof(GetReview), new { id = review.Id }, review.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Update a review (author only or SystemAdmin for moderation).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<ReviewResponse>> PutReview(Guid id, [FromBody] UpdateReviewRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (id != request.Id)
        {
            return BadRequest();
        }

        var isAdmin = User.IsInRole("SystemAdmin");

        // IDOR ownership check: fetch review first, then verify caller is the author
        var existing = await _reviewService.GetReviewByIdAsync(id);
        if (existing == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Review with id {id} not found."
            });

        if (!isAdmin && existing.AuthorId != User.GetUserId())
            return Forbid();

        try
        {
            var review = await _reviewService.UpdateReviewAsync(id, request.ToBllDto(), userId.Value, isAdmin);
            return Ok(review.ToResponse());
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Delete a review (author only or SystemAdmin for moderation).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");

        // IDOR ownership check: fetch review first, then verify caller is the author
        var existing = await _reviewService.GetReviewByIdAsync(id);
        if (existing == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Review with id {id} not found."
            });

        if (!isAdmin && existing.AuthorId != User.GetUserId())
            return Forbid();

        try
        {
            await _reviewService.DeleteReviewAsync(id, userId.Value, isAdmin);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = ex.Message
            });
        }
    }
}
