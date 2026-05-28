using Asp.Versioning;
using Fleet.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Fleet;
using Shared.Contracts.Fleet.DTOs;

namespace Fleet.WebHost.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/fleet")]
[ApiController]
public class InternalFleetController : ControllerBase
{
    private readonly IFleetModuleApi _fleetApi;
    private readonly ISystemAdminFleetService _adminService;

    public InternalFleetController(
        IFleetModuleApi fleetApi,
        ISystemAdminFleetService adminService)
    {
        _fleetApi = fleetApi;
        _adminService = adminService;
    }

    // ── IFleetModuleApi endpoints (for Booking service) ─────────────

    [HttpGet("aircraft/{id:guid}")]
    public async Task<ActionResult<AircraftBasicDto>> GetAircraftById(Guid id)
    {
        var aircraft = await _fleetApi.GetAircraftByIdAsync(id);
        return aircraft == null ? NotFound() : Ok(aircraft);
    }

    [HttpPost("aircraft/batch")]
    public async Task<ActionResult<Dictionary<Guid, AircraftBasicDto>>> GetAircraftByIds(
        [FromBody] List<Guid> ids)
    {
        var aircraft = await _fleetApi.GetAircraftsByIdsAsync(ids);
        return Ok(aircraft);
    }

    [HttpGet("aircraft/{id:guid}/availability-check")]
    public async Task<ActionResult<bool>> CheckAvailability(
        Guid id, [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        // Model binding produces DateTime with Kind=Unspecified, which Npgsql
        // refuses to write into PostgreSQL "timestamp with time zone" columns.
        // The Booking service serialises with ISO-8601 round-trip ('O'), which
        // preserves the trailing 'Z', so values arriving here are guaranteed
        // to represent UTC moments — we just need to tag them as such.
        var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var endUtc   = DateTime.SpecifyKind(end,   DateTimeKind.Utc);

        var result = await _fleetApi.CheckAircraftAvailabilityAsync(id, startUtc, endUtc);
        return Ok(result);
    }

    [HttpGet("aircraft/count/company/{companyId:guid}")]
    public async Task<ActionResult<int>> GetAircraftCountByCompany(Guid companyId)
        => Ok(await _fleetApi.GetAircraftCountByCompanyAsync(companyId));

    [HttpGet("aircraft/company/{companyId:guid}")]
    public async Task<ActionResult<List<AircraftBasicDto>>> GetAircraftByCompany(Guid companyId)
        => Ok(await _fleetApi.GetAircraftsByCompanyAsync(companyId));

    [HttpGet("aircraft/count")]
    public async Task<ActionResult<int>> GetTotalAircraftCount()
        => Ok(await _fleetApi.GetTotalAircraftCountAsync());

    [HttpGet("airports/count")]
    public async Task<ActionResult<int>> GetTotalAirportsCount()
        => Ok(await _fleetApi.GetTotalAirportsCountAsync());

    // ── Admin endpoints (for WebApp SystemAdmin panel) ──────────────

    [HttpGet("admin/aircraft")]
    public async Task<IActionResult> GetAllAircraft(
        [FromQuery] string? search, [FromQuery] Guid? tenantId,
        [FromQuery] bool? available, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _adminService.GetAllAircraftAsync(search, tenantId, available, page, pageSize));

    [HttpGet("admin/airports")]
    public async Task<IActionResult> GetAirports(
        [FromQuery] string? search, [FromQuery] bool showDeleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _adminService.GetAirportsAsync(search, showDeleted, page, pageSize));

    [HttpGet("admin/airports/{id:guid}")]
    public async Task<IActionResult> GetAirportForEdit(Guid id)
    {
        var airport = await _adminService.GetAirportForEditAsync(id);
        return airport == null ? NotFound() : Ok(airport);
    }

    [HttpGet("admin/airports/exists")]
    public async Task<ActionResult<bool>> AirportExistsByIcao(
        [FromQuery] string icaoCode, [FromQuery] Guid? excludeId)
        => Ok(await _adminService.AirportExistsByIcaoCodeAsync(icaoCode, excludeId));

    [HttpGet("admin/airports/{id:guid}/has-aircraft")]
    public async Task<ActionResult<bool>> HasActiveAircraftAtAirport(Guid id)
        => Ok(await _adminService.HasActiveAircraftAtAirportAsync(id));
}
