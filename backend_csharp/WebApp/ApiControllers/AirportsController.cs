using System.Net;
using System.Security.Claims;
using App.Application.Interfaces;
using WebApp.v1;
using WebApp.v1.Mappers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.ApiControllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Normal,CompanyOwner,SystemAdmin")]
public class AirportsController : ControllerBase
{
    private readonly IAirportService _airportService;
    private readonly ILogger<AirportsController> _logger;

    public AirportsController(
        IAirportService airportService,
        ILogger<AirportsController> logger)
    {
        _airportService = airportService;
        _logger = logger;
    }

    /// <summary>
    /// Get all airports (public).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<AirportResponse>>> GetAirports()
    {
        var airports = await _airportService.GetAllAirportsAsync();
        return Ok(airports.ToResponse());
    }

    /// <summary>
    /// Search airports by name, ICAO/IATA code or city (public).
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<AirportResponse>>> SearchAirports([FromQuery] string? term)
    {
        var airports = await _airportService.SearchAirportsAsync(term);
        return Ok(airports.ToResponse());
    }

    /// <summary>
    /// Get a single airport by ID (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<AirportResponse>> GetAirport(Guid id)
    {
        var airport = await _airportService.GetAirportByIdAsync(id);
        if (airport == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Airport with id {id} not found."
            });

        return Ok(airport.ToResponse());
    }

    /// <summary>
    /// Get airport by ICAO code (public).
    /// </summary>
    [HttpGet("icao/{icaoCode}")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<AirportResponse>> GetAirportByIcao(string icaoCode)
    {
        var airport = await _airportService.GetAirportByIcaoCodeAsync(icaoCode);
        if (airport == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"Airport with ICAO code '{icaoCode}' not found."
            });

        return Ok(airport.ToResponse());
    }

    /// <summary>
    /// Create a new airport (SystemAdmin only).
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<AirportResponse>> PostAirport([FromBody] CreateAirportRequest request)
    {
        try
        {
            var airport = await _airportService.CreateAirportAsync(request.ToBllDto());
            return CreatedAtAction(nameof(GetAirport), new { id = airport.Id }, airport.ToResponse());
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
    /// Update an airport (SystemAdmin only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<AirportResponse>> PutAirport(Guid id, [FromBody] UpdateAirportRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest();
        }

        try
        {
            var airport = await _airportService.UpdateAirportAsync(id, request.ToBllDto());
            return Ok(airport.ToResponse());
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
    /// Delete an airport (SystemAdmin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> DeleteAirport(Guid id)
    {
        try
        {
            await _airportService.DeleteAirportAsync(id);
            return NoContent();
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
    /// Restore a soft-deleted airport (SystemAdmin only).
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
    [ProducesResponseType<RestApiErrorResponse>((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> RestoreAirport(Guid id)
    {
        var restored = await _airportService.RestoreAirportAsync(id);
        if (!restored)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = $"Airport with id {id} could not be restored."
            });

        return NoContent();
    }
}
