using System.Net;
using System.Security.Claims;
using Booking.Api.DTOs;
using Booking.Api.Mappers;
using Booking.Application.Interfaces;
using Shared.Contracts.Common;
using Asp.Versioning;
using Shared.Kernel.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Booking.Api.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        IBookingService bookingService,
        ITenantContext tenantContext,
        ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Get the current user's own bookings. (Normal, CompanyOwner)
    /// </summary>
    [HttpGet("my")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetMyBookings()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var bookings = await _bookingService.GetAllForPilotAsync(userId.Value);
        return Ok(bookings.ToResponse());
    }

    /// <summary>
    /// Get all bookings for the current user's company (CompanyOwner only).
    /// </summary>
    [HttpGet("company")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetCompanyBookings()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: caller must be a member of the resolved company
        if (!await _tenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        var bookings = await _bookingService.GetAllForCompanyAsync(tenantId.Value);
        return Ok(bookings.ToResponse());
    }

    /// <summary>
    /// Get a single booking by ID (owned by the user, or user is company owner).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<BookingResponse>> GetBooking(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // Fetch booking without ownership filter first
        var booking = await _bookingService.GetByIdAsync(id, null, null);
        if (booking == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Booking with id {id} not found."
            });

        // IDOR ownership check: the current user must be the pilot or a company member
        var isCompanyMember = await _tenantContext.IsUserInCompanyAsync(booking.CompanyId, userId.Value);
        if (booking.PilotId != userId.Value && !isCompanyMember)
        {
            return Forbid();
        }

        return Ok(booking.ToResponse());
    }

    /// <summary>
    /// Request a new booking for an aircraft. (Normal, CompanyOwner only — SystemAdmin cannot book)
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<BookingResponse>> PostBooking([FromBody] CreateBookingRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // NOTE: We rely on RequestBookingAsync to perform ALL validations and throw
        // InvalidOperationException with a SPECIFIC reason (overlap / no license /
        // no insurance / aircraft missing / availability blocked). The previous
        // pre-call to ValidateBookingAsync collapsed multiple causes into one
        // generic message ("Aircraft is not available for the selected dates."),
        // which made debugging 400 responses very hard.
        try
        {
            var booking = await _bookingService.RequestBookingAsync(request.ToBllDto(), userId.Value);
            return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex,
                "Booking validation failed for user {UserId}, aircraft {AircraftId} ({Start}–{End}): {Reason}",
                userId.Value, request.AircraftId, request.StartDateTime, request.EndDateTime, ex.Message);
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while creating booking for user {UserId}, aircraft {AircraftId}",
                userId.Value, request.AircraftId);
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = $"Unable to create booking: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Update a booking (pilot who owns the booking, only Pending/Requested). (Normal, CompanyOwner only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BookingResponse>> UpdateBooking(Guid id, [FromBody] UpdateBookingRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (id != request.Id)
        {
            return BadRequest();
        }

        // IDOR ownership check: fetch booking first, then verify caller is the pilot
        var existing = await _bookingService.GetByIdAsync(id, null, null);
        if (existing == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Booking with id {id} not found."
            });

        if (existing.PilotId != User.GetUserId())
            return Forbid();

        try
        {
            var booking = await _bookingService.UpdateBookingAsync(request.ToBllDto(), userId.Value);
            return Ok(booking.ToResponse());
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
    /// Approve a booking (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BookingResponse>> ApproveBooking(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: only company members can approve bookings
        if (!await _tenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var booking = await _bookingService.ApproveAsync(id, tenantId.Value);
            return Ok(booking.ToResponse());
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
    /// Reject a booking with a reason (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BookingResponse>> RejectBooking(Guid id, [FromBody] RejectBookingRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: only company members can reject bookings
        if (!await _tenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var booking = await _bookingService.RejectAsync(id, tenantId.Value, request.Reason ?? "");
            return Ok(booking.ToResponse());
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
    /// Mark a booking as complete (CompanyOwner only, tenant-scoped).
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BookingResponse>> CompleteBooking(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        // IDOR ownership check: only company members can complete bookings
        if (!await _tenantContext.IsUserInCompanyAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var booking = await _bookingService.CompleteAsync(id, tenantId.Value);
            return Ok(booking.ToResponse());
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
    /// Cancel a booking (booking owner or company user). (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BookingResponse>> CancelBooking(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // IDOR ownership check: fetch booking first, then verify caller is the pilot or company member
        var existing = await _bookingService.GetByIdAsync(id, null, null);
        if (existing == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Booking with id {id} not found."
            });

        var isCompanyMember = await _tenantContext.IsUserInCompanyAsync(existing.CompanyId, userId.Value);
        if (existing.PilotId != userId.Value && !isCompanyMember)
        {
            return Forbid();
        }

        try
        {
            var booking = await _bookingService.CancelAsync(id, userId.Value);
            return Ok(booking.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new RestApiErrorResponse
            {
                Status = HttpStatusCode.Unauthorized,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Pay for a booking (booking owner only). (Normal, CompanyOwner only)
    /// </summary>
    [HttpPost("{id:guid}/pay")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<BookingResponse>> PayBooking(Guid id, [FromBody] PaymentRequest payment)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // IDOR ownership check: only the pilot who owns the booking can pay for it
        var existing = await _bookingService.GetByIdAsync(id, null, null);
        if (existing == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Booking with id {id} not found."
            });

        if (existing.PilotId != userId.Value)
        {
            return Forbid();
        }

        // Ensure transaction ID is set
        if (string.IsNullOrEmpty(payment.TransactionId))
            payment.TransactionId = Guid.NewGuid().ToString();

        try
        {
            var booking = await _bookingService.ConfirmPaymentAsync(id, payment.ToBllDto(), userId.Value);
            return Ok(booking.ToResponse());
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
}
