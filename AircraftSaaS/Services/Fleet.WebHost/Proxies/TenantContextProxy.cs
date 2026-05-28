using System.Net.Http.Json;
using Shared.Contracts.Common;

namespace Fleet.WebHost.Proxies;

/// <summary>
/// HTTP proxy implementation of ITenantContext for the Fleet microservice.
/// Forwards calls to the Users microservice's internal/tenant endpoints.
/// </summary>
public class TenantContextProxy : ITenantContext
{
    private readonly HttpClient _http;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IRequestContextProvider _requestContextProvider;
    private readonly ILogger<TenantContextProxy> _logger;
    private Guid? _currentTenantId;

    public TenantContextProxy(
        HttpClient http,
        ICurrentUserProvider currentUserProvider,
        IRequestContextProvider requestContextProvider,
        ILogger<TenantContextProxy> logger)
    {
        _http = http;
        _currentUserProvider = currentUserProvider;
        _requestContextProvider = requestContextProvider;
        _logger = logger;
    }

    public Guid? GetCurrentTenantId()
    {
        if (_currentTenantId.HasValue) return _currentTenantId;

        var headerTenantId = _requestContextProvider.GetHeaderValue("X-Tenant-Id");
        if (!string.IsNullOrEmpty(headerTenantId) && Guid.TryParse(headerTenantId, out var tid))
        {
            _currentTenantId = tid;
            return tid;
        }

        var cookieValue = _requestContextProvider.GetCookieValue("SelectedCompanyId");
        if (!string.IsNullOrEmpty(cookieValue) && Guid.TryParse(cookieValue, out var ctid))
        {
            _currentTenantId = ctid;
            return ctid;
        }

        return null;
    }

    public void SetCurrentTenant(Guid companyId)
    {
        _currentTenantId = companyId;
        _requestContextProvider.SetCookie("SelectedCompanyId", companyId.ToString(),
            expiryDays: 30, httpOnly: true);
    }

    public async Task<bool> IsUserInCompanyAsync(Guid companyId, Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<bool>(
                $"api/v1/internal/tenant/user-in-company?companyId={companyId}&userId={userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check user {UserId} in company {CompanyId}", userId, companyId);
            return false;
        }
    }

    public async Task<bool> IsUserCompanyOwnerAsync(Guid companyId, Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<bool>(
                $"api/v1/internal/tenant/user-company-owner?companyId={companyId}&userId={userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check company owner {UserId} in {CompanyId}", userId, companyId);
            return false;
        }
    }

    public async Task<Guid?> ResolveOrAutoSetTenantAsync(Guid userId)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId.HasValue) return tenantId;

        var companyIds = await GetUserCompanyIdsAsync(userId);
        var first = companyIds.FirstOrDefault();
        if (first == Guid.Empty) return null;

        SetCurrentTenant(first);
        return first;
    }

    public Guid? GetCurrentUserId() => _currentUserProvider.GetCurrentUserId();

    public async Task<string?> GetUserRoleInCompanyAsync(Guid companyId, Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<string>(
                $"api/v1/internal/tenant/user-role?companyId={companyId}&userId={userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user role for {UserId} in {CompanyId}", userId, companyId);
            return null;
        }
    }

    public async Task<IEnumerable<Guid>> GetUserCompanyIdsAsync(Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<Guid>>(
                $"api/v1/internal/tenant/user-companies/{userId}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get company IDs for user {UserId}", userId);
            return Enumerable.Empty<Guid>();
        }
    }

    public async Task<IEnumerable<UserCompanySummary>> GetUserCompanySummariesAsync(Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<UserCompanySummary>>(
                $"api/v1/internal/tenant/user-company-summaries/{userId}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get company summaries for user {UserId}", userId);
            return Enumerable.Empty<UserCompanySummary>();
        }
    }

    public async Task<Guid?> ResolveTenantIdFromSlugAsync(string slug)
    {
        try
        {
            return await _http.GetFromJsonAsync<Guid?>(
                $"api/v1/internal/tenant/resolve-slug/{slug}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve slug '{Slug}'", slug);
            return null;
        }
    }

    public string? GetCurrentTenantSlug()
    {
        var path = _requestContextProvider.GetRequestPath();
        if (string.IsNullOrEmpty(path)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            path, @"^/([a-z0-9-]+)(?:/|$)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
