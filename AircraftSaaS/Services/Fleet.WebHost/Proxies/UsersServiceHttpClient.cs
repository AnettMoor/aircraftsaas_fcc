using System.Net.Http.Json;
using Shared.Contracts.Common;
using Shared.Contracts.Users;
using Shared.Contracts.Users.DTOs;

namespace Fleet.WebHost.Proxies;

public class UsersServiceHttpClient : IUsersModuleApi
{
    private readonly HttpClient _http;
    private readonly ILogger<UsersServiceHttpClient> _logger;

    public UsersServiceHttpClient(HttpClient http, ILogger<UsersServiceHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<UserBasicDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<UserBasicDto>(
                $"api/v1/internal/users/{userId}", ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get user {UserId} from Users service", userId);
            return null;
        }
    }

    public async Task<Dictionary<Guid, UserBasicDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/v1/internal/users/batch", userIds.ToList(), ct);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<Dictionary<Guid, UserBasicDto>>(ct) ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get users batch from Users service");
            return new Dictionary<Guid, UserBasicDto>();
        }
    }

    public async Task<bool> CheckUserLicenseAsync(
        Guid userId, string requiredLicenseType, DateTime asOfDate, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<bool>(
                $"api/v1/internal/users/{userId}/license-check" +
                $"?licenseType={requiredLicenseType}&asOfDate={asOfDate:O}", ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check license for user {UserId}", userId);
            return false;
        }
    }

    public async Task<CompanyBasicDto?> GetCompanyByIdAsync(Guid companyId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<CompanyBasicDto>(
                $"api/v1/internal/companies/{companyId}", ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get company {CompanyId}", companyId);
            return null;
        }
    }

    public async Task<Dictionary<Guid, CompanyBasicDto>> GetCompaniesByIdsAsync(
        IEnumerable<Guid> companyIds, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/v1/internal/companies/batch", companyIds.ToList(), ct);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<Dictionary<Guid, CompanyBasicDto>>(ct) ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get companies batch");
            return new Dictionary<Guid, CompanyBasicDto>();
        }
    }

    public async Task<List<UserBasicDto>> GetCompanyUsersAsync(Guid companyId, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<UserBasicDto>>(
                $"api/v1/internal/companies/{companyId}/users", ct) ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get company users for {CompanyId}", companyId);
            return new List<UserBasicDto>();
        }
    }

    public async Task<List<CompanySelectItemDto>> GetActiveCompaniesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<CompanySelectItemDto>>(
                "api/v1/internal/companies/active", ct) ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get active companies");
            return new List<CompanySelectItemDto>();
        }
    }
}
