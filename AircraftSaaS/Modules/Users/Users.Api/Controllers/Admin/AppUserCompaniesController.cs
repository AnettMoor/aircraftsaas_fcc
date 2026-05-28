using System.Net;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Asp.Versioning;
using Shared.Contracts.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Users.Api.Controllers.Admin;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SystemAdmin")]
public class AppUserCompaniesController : ControllerBase
{
    private readonly IAppUserCompanyService _appUserCompanyService;

    public AppUserCompaniesController(IAppUserCompanyService appUserCompanyService)
    {
        _appUserCompanyService = appUserCompanyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppUserCompanyDto>>> GetAll()
    {
        return Ok(await _appUserCompanyService.GetAllAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppUserCompanyDto>> Get(Guid id)
    {
        var dto = await _appUserCompanyService.GetByIdAsync(id);
        if (dto == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"AppUserCompany with id {id} not found."
            });
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<AppUserCompanyDto>> Create([FromBody] CreateAppUserCompanyDto dto)
    {
        try
        {
            var created = await _appUserCompanyService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AppUserCompanyDto>> Update(Guid id, [FromBody] UpdateAppUserCompanyDto dto)
    {
        try
        {
            var updated = await _appUserCompanyService.UpdateAsync(id, dto);
            return Ok(updated);
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _appUserCompanyService.DeleteAsync(id);
            return NoContent();
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
