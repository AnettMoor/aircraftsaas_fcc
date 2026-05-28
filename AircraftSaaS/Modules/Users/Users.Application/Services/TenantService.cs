using Shared.Contracts.Common;
using Users.Application.Contracts;
using Users.Application.Interfaces;
using Users.Domain.Entities;
using Users.Domain.Enums;

namespace Users.Application.Services;

internal sealed class TenantService : ITenantService, ITenantContext
{
    private readonly IUsersUOW _uow;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IRequestContextProvider _requestContextProvider;
    private Guid? _currentTenantId;
    
    public TenantService(IUsersUOW uow, ICurrentUserProvider currentUserProvider, IRequestContextProvider requestContextProvider)
    {
        _uow = uow;
        _currentUserProvider = currentUserProvider;
        _requestContextProvider = requestContextProvider;
    }
    
    public Guid? GetCurrentTenantId()
    {
        if (_currentTenantId.HasValue)
            return _currentTenantId;
        
        // Check X-Tenant-Id header FIRST (explicitly set by Vue/SPA clients)
        var headerTenantId = _requestContextProvider.GetHeaderValue("X-Tenant-Id");
        if (!string.IsNullOrEmpty(headerTenantId) && Guid.TryParse(headerTenantId, out var tenantId))
        {
            _currentTenantId = tenantId;
            return tenantId;
        }
        
        // Fall back to cookie (persists across MVC requests)
        var cookieValue = _requestContextProvider.GetCookieValue("SelectedCompanyId");
        if (!string.IsNullOrEmpty(cookieValue) && Guid.TryParse(cookieValue, out var cookieTenantId))
        {
            _currentTenantId = cookieTenantId;
            return cookieTenantId;
        }
        
        // Check JWT claim
        if (_currentUserProvider.IsAuthenticated())
        {
            var companyClaim = _currentUserProvider.GetClaimValue("companyId");
            if (companyClaim != null && Guid.TryParse(companyClaim, out var companyId))
            {
                return companyId;
            }
        }
        
        return null;
    }
    
    public string? GetCurrentTenantSlug()
    {
        var path = _requestContextProvider.GetRequestPath();
        if (string.IsNullOrEmpty(path))
            return null;
            
        var match = System.Text.RegularExpressions.Regex.Match(
            path, @"^/([a-z0-9-]+)(?:/|$)");
            
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return null;
    }
    
    public async Task<Guid?> ResolveTenantIdFromSlugAsync(string slug)
    {
        var company = await _uow.CompanyRepository.GetBySlugAsync(slug);
        return company?.Id;
    }
    
    public async Task<Guid?> ResolveTenantIdFromHostAsync(string host)
    {
        // For subdomain-based routing: acme.platform.com
        var slug = host.Split('.')[0];
        return await ResolveTenantIdFromSlugAsync(slug);
    }
    
    public void SetCurrentTenant(Guid tenantId)
    {
        _currentTenantId = tenantId;
        
        // Also set a cookie so it persists across requests
        _requestContextProvider.SetCookie("SelectedCompanyId", tenantId.ToString(), expiryDays: 30, httpOnly: true);
    }
    
    public async Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId)
    {
        return await _uow.CompanyRepository.IsUserInCompanyAsync(companyId, userId);
    }
    
    // check if user has companyowner role
    public async Task<bool> IsUserCompanyOwnerAsync(Guid companyId, Guid userId)
    {
        return await _uow.CompanyRepository.IsCompanyOwnerAsync(userId, companyId);
    }

    public async Task<EAppUserRoleInCompany?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId)
    {
        return await _uow.CompanyRepository.GetUserRoleInCompanyAsync(companyId, userId);
    }

    /// <summary>
    /// ITenantContext-compatible overload — returns the role as a string
    /// so cross-module consumers don't need a reference to Users.Domain.
    /// </summary>
    async Task<string?> ITenantContext.GetUserRoleInCompanyAsync(Guid companyId, Guid userId)
    {
        var role = await GetUserRoleInCompanyAsync(companyId, userId);
        return role?.ToString();
    }

    public async Task<IEnumerable<AppUserCompany>> GetUserCompaniesAsync(Guid userId)
    {
        return await _uow.CompanyRepository.GetUserCompaniesAsync(userId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetUserCompanyIdsAsync(Guid userId)
    {
        var companies = await GetUserCompaniesAsync(userId);
        return companies.Select(c => c.CompanyId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserCompanySummary>> GetUserCompanySummariesAsync(Guid userId)
    {
        var companies = await GetUserCompaniesAsync(userId);
        return companies.Select(c => new UserCompanySummary(
            c.CompanyId,
            c.Company?.CompanyName ?? "Unknown",
            c.Role.ToString(),
            c.Company?.IsActive ?? false));
    }
    
    public Guid? GetCurrentUserId()
    {
        return _currentUserProvider.GetCurrentUserId();
    }
    
    public async Task<Guid?> ResolveOrAutoSetTenantAsync(Guid userId)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId.HasValue)
            return tenantId;
        
        var companies = await GetUserCompaniesAsync(userId);
        var first = companies.FirstOrDefault();
        if (first == null)
            return null;
        
        SetCurrentTenant(first.CompanyId);
        return first.CompanyId;
    }
}
