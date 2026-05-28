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
public class ContactTypesController : ControllerBase
{
    private readonly IContactTypeService _contactTypeService;

    public ContactTypesController(IContactTypeService contactTypeService)
    {
        _contactTypeService = contactTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContactTypeDto>>> GetAll()
    {
        return Ok(await _contactTypeService.GetAllAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactTypeDto>> Get(Guid id)
    {
        var dto = await _contactTypeService.GetByIdAsync(id);
        if (dto == null)
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = $"ContactType with id {id} not found."
            });
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ContactTypeDto>> Create([FromBody] CreateContactTypeDto dto)
    {
        try
        {
            var created = await _contactTypeService.CreateAsync(dto);
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
    public async Task<ActionResult<ContactTypeDto>> Update(Guid id, [FromBody] UpdateContactTypeDto dto)
    {
        try
        {
            var updated = await _contactTypeService.UpdateAsync(id, dto);
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
            await _contactTypeService.DeleteAsync(id);
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
