using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Common;
using Users.Application.Interfaces;

namespace Users.WebHost.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/tenant")]
[ApiController]
public class InternalTenantController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantContext _tenantContext;

    public InternalTenantController(ITenantService tenantService, ITenantContext tenantContext)
    {
        _tenantService = tenantService;
        _tenantContext = tenantContext;
    }

    [HttpGet("resolve-slug/{slug}")]
    public async Task<ActionResult<Guid?>> ResolveSlug(string slug)
    {
        var tenantId = await _tenantContext.ResolveTenantIdFromSlugAsync(slug);
        return Ok(tenantId);
    }

    [HttpGet("user-companies/{userId:guid}")]
    public async Task<ActionResult<List<Guid>>> GetUserCompanyIds(Guid userId)
    {
        var ids = await _tenantContext.GetUserCompanyIdsAsync(userId);
        return Ok(ids.ToList());
    }

    [HttpGet("user-in-company")]
    public async Task<ActionResult<bool>> IsUserInCompany(
        [FromQuery] Guid companyId, [FromQuery] Guid userId)
    {
        var result = await _tenantContext.IsUserInCompanyAsync(companyId, userId);
        return Ok(result);
    }

    [HttpGet("user-company-owner")]
    public async Task<ActionResult<bool>> IsUserCompanyOwner(
        [FromQuery] Guid companyId, [FromQuery] Guid userId)
    {
        var result = await _tenantContext.IsUserCompanyOwnerAsync(companyId, userId);
        return Ok(result);
    }

    [HttpGet("user-company-summaries/{userId:guid}")]
    public async Task<ActionResult<List<UserCompanySummary>>> GetUserCompanySummaries(Guid userId)
    {
        var summaries = await _tenantContext.GetUserCompanySummariesAsync(userId);
        return Ok(summaries.ToList());
    }

    [HttpGet("user-role")]
    public async Task<ActionResult<string?>> GetUserRoleInCompany(
        [FromQuery] Guid companyId, [FromQuery] Guid userId)
    {
        var role = await _tenantContext.GetUserRoleInCompanyAsync(companyId, userId);
        return Ok(role);
    }

    [HttpGet("company-active/{companyId:guid}")]
    public async Task<ActionResult<bool>> IsCompanyActive(Guid companyId)
    {
        var companyService = HttpContext.RequestServices.GetRequiredService<ICompanyService>();
        var result = await companyService.IsCompanyActiveAsync(companyId);
        return Ok(result);
    }

    /// <summary>
    /// Returns slug → companyId mapping for all active companies.
    /// Used by the monolith during Fleet seeding to map company slugs to Guids.
    /// </summary>
    [HttpGet("company-slug-mapping")]
    public ActionResult<Dictionary<string, Guid>> GetCompanySlugMapping()
    {
        var mapping = Users.Infrastructure.UsersModule.GetCompanySlugMapping(
            HttpContext.RequestServices);
        return Ok(mapping);
    }
}
