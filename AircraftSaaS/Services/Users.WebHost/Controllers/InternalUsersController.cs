using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Users;
using Shared.Contracts.Users.DTOs;

namespace Users.WebHost.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal")]
[ApiController]
public class InternalUsersController : ControllerBase
{
    private readonly IUsersModuleApi _usersApi;

    public InternalUsersController(IUsersModuleApi usersApi)
    {
        _usersApi = usersApi;
    }

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<UserBasicDto>> GetUserById(Guid id)
    {
        var user = await _usersApi.GetUserByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost("users/batch")]
    public async Task<ActionResult<Dictionary<Guid, UserBasicDto>>> GetUsersByIds(
        [FromBody] List<Guid> ids)
    {
        var users = await _usersApi.GetUsersByIdsAsync(ids);
        return Ok(users);
    }

    [HttpGet("users/{id:guid}/license-check")]
    public async Task<ActionResult<bool>> CheckUserLicense(
        Guid id,
        [FromQuery] string licenseType,
        [FromQuery] DateTime asOfDate)
    {
        // Model binding produces DateTime with Kind=Unspecified, which Npgsql
        // refuses to write into PostgreSQL "timestamp with time zone" columns.
        // The caller serialises with ISO-8601 round-trip ('O') and a 'Z' suffix,
        // so values arriving here always represent UTC moments — we just need
        // to tag them as such before they reach EF Core.
        var asOfUtc = DateTime.SpecifyKind(asOfDate, DateTimeKind.Utc);
        var result = await _usersApi.CheckUserLicenseAsync(id, licenseType, asOfUtc);
        return Ok(result);
    }

    [HttpGet("companies/{id:guid}")]
    public async Task<ActionResult<CompanyBasicDto>> GetCompanyById(Guid id)
    {
        var company = await _usersApi.GetCompanyByIdAsync(id);
        return company == null ? NotFound() : Ok(company);
    }

    [HttpPost("companies/batch")]
    public async Task<ActionResult<Dictionary<Guid, CompanyBasicDto>>> GetCompaniesByIds(
        [FromBody] List<Guid> ids)
    {
        var companies = await _usersApi.GetCompaniesByIdsAsync(ids);
        return Ok(companies);
    }

    [HttpGet("companies/{id:guid}/users")]
    public async Task<ActionResult<List<UserBasicDto>>> GetCompanyUsers(Guid id)
    {
        var users = await _usersApi.GetCompanyUsersAsync(id);
        return Ok(users);
    }

    [HttpGet("companies/active")]
    public async Task<ActionResult> GetActiveCompanies()
    {
        var companies = await _usersApi.GetActiveCompaniesAsync();
        return Ok(companies);
    }
}
