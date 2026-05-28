using System.Net.Http.Json;
using Shared.Contracts.Common;
using Users.Application.DTOs;
using Users.Application.Interfaces;

namespace WebApp.Proxies;

/// <summary>
/// HTTP proxy for ISystemAdminUsersService — delegates all admin operations
/// to the Users microservice via REST calls.
/// </summary>
public class AdminServiceProxy : ISystemAdminUsersService
{
    private readonly HttpClient _http;
    private readonly ILogger<AdminServiceProxy> _logger;

    public AdminServiceProxy(HttpClient http, ILogger<AdminServiceProxy> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────
    public async Task<SystemAdminDashboardDto> GetDashboardAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<SystemAdminDashboardDto>(
                "api/v1/internal/admin/dashboard") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get admin dashboard");
            return new SystemAdminDashboardDto();
        }
    }

    // ── Users ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<SystemAdminUserDto>> GetUsersAsync(
        string? search, bool? deactivated, int page, int pageSize)
    {
        try
        {
            var url = $"api/v1/internal/admin/users?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (deactivated.HasValue) url += $"&deactivated={deactivated}";

            return await _http.GetFromJsonAsync<PagedResult<SystemAdminUserDto>>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get admin users");
            return new PagedResult<SystemAdminUserDto>();
        }
    }

    public async Task<(bool Succeeded, string? Error)> DeactivateUserAsync(Guid userId, Guid currentUserId)
    {
        try
        {
            var response = await _http.PostAsync(
                $"api/v1/internal/admin/users/{userId}/deactivate?currentUserId={currentUserId}", null);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, error?.Error ?? "Failed to deactivate user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate user {UserId}", userId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Succeeded, string? Error)> ReactivateUserAsync(Guid userId)
    {
        try
        {
            var response = await _http.PostAsync(
                $"api/v1/internal/admin/users/{userId}/reactivate", null);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, error?.Error ?? "Failed to reactivate user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reactivate user {UserId}", userId);
            return (false, ex.Message);
        }
    }

    // ── Roles ─────────────────────────────────────────────────────────────
    public async Task<UserRolesDataDto?> GetUserRolesDataAsync(Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<UserRolesDataDto>(
                $"api/v1/internal/admin/users/{userId}/roles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get roles data for user {UserId}", userId);
            return null;
        }
    }

    public async Task UpdateUserRoleAsync(Guid userId, string selectedRole)
    {
        try
        {
            await _http.PutAsJsonAsync(
                $"api/v1/internal/admin/users/{userId}/roles",
                new { SelectedRole = selectedRole });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role for user {UserId}", userId);
        }
    }

    // ── Change Company ────────────────────────────────────────────────────
    public async Task<ChangeUserCompanyDataDto?> GetChangeUserCompanyDataAsync(Guid userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ChangeUserCompanyDataDto>(
                $"api/v1/internal/admin/users/{userId}/change-company");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get change company data for {UserId}", userId);
            return null;
        }
    }

    public async Task<string?> ValidateChangeUserCompanyAsync(Guid userId)
    {
        try
        {
            var response = await _http.GetAsync(
                $"api/v1/internal/admin/users/{userId}/change-company");
            if (response.IsSuccessStatusCode) return null;
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return error?.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate change company for {UserId}", userId);
            return ex.Message;
        }
    }

    public async Task<(bool Succeeded, string? Error, string? CompanyName)> ChangeUserCompanyAsync(
        Guid userId, Guid companyId, string updatedBy)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"api/v1/internal/admin/users/{userId}/change-company",
                new { CompanyId = companyId, UpdatedBy = updatedBy });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChangeCompanyResult>();
                return (true, null, result?.CompanyName);
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, error?.Error ?? "Failed to change company", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change company for {UserId}", userId);
            return (false, ex.Message, null);
        }
    }

    // ── Tenants ───────────────────────────────────────────────────────────
    public async Task<TenantsListDto> GetTenantsAsync(
        string? search, bool? active, int page, int pageSize)
    {
        try
        {
            var url = $"api/v1/internal/admin/tenants?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (active.HasValue) url += $"&active={active}";

            return await _http.GetFromJsonAsync<TenantsListDto>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenants list");
            return new TenantsListDto();
        }
    }

    public async Task<(bool Succeeded, string Status, string? Error)> ToggleTenantActiveAsync(
        Guid companyId, string updatedBy)
    {
        try
        {
            var response = await _http.PostAsync(
                $"api/v1/internal/admin/tenants/{companyId}/toggle?updatedBy={Uri.EscapeDataString(updatedBy)}",
                null);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ToggleResult>();
                return (true, result?.Status ?? "toggled", null);
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, "", error?.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle tenant {CompanyId}", companyId);
            return (false, "", ex.Message);
        }
    }

    // ── Audit Logs ────────────────────────────────────────────────────────
    public async Task<AuditLogListDto> GetAuditLogsAsync(
        string? search, string? entity, string? action, Guid? tenantId, int page, int pageSize)
    {
        try
        {
            var url = $"api/v1/internal/admin/audit-logs?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(entity)) url += $"&entity={Uri.EscapeDataString(entity)}";
            if (!string.IsNullOrEmpty(action)) url += $"&action={Uri.EscapeDataString(action)}";
            if (tenantId.HasValue) url += $"&tenantId={tenantId}";

            return await _http.GetFromJsonAsync<AuditLogListDto>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs");
            return new AuditLogListDto();
        }
    }

    // ── Create Tenant ────────────────────────────────────────────────────
    public async Task<bool> SlugExistsAsync(string slug)
    {
        try
        {
            return await _http.GetFromJsonAsync<bool>(
                $"api/v1/internal/admin/tenants/slug-exists?slug={Uri.EscapeDataString(slug)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check slug existence");
            return false;
        }
    }

    public async Task<Guid> CreateTenantAsync(CreateTenantDto dto, string createdBy)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/internal/admin/tenants/create",
            new { Dto = dto, CreatedBy = createdBy, OwnerUserId = (Guid?)null });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateTenantResult>();
        return result?.CompanyId ?? Guid.Empty;
    }

    public async Task AssignTenantOwnerAsync(Guid companyId, Guid ownerUserId, string createdBy)
    {
        // This is handled as part of CreateTenantAsync via the OwnerUserId parameter
        // For standalone calls, we make a separate request
        await _http.PostAsJsonAsync(
            "api/v1/internal/admin/tenants/create",
            new
            {
                Dto = new CreateTenantDto(),
                CreatedBy = createdBy,
                OwnerUserId = ownerUserId
            });
    }

    public async Task<IEnumerable<UserSelectItemDto>> GetAllUsersForSelectAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<UserSelectItemDto>>(
                "api/v1/internal/admin/users-for-select") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users for select");
            return new List<UserSelectItemDto>();
        }
    }

    public string GenerateSlug(string name)
    {
        // Generate slug locally — simple enough not to require HTTP call
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("'", "")
            .Replace("\"", "");
    }

    // ── Create User ──────────────────────────────────────────────────────
    public async Task<CreateUserResultDto> CreateUserAsync(CreateSystemUserDto dto, string createdBy)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/v1/internal/admin/users/create",
                new { Dto = dto, CreatedBy = createdBy });

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<CreateUserResultDto>()
                       ?? new CreateUserResultDto { Succeeded = false, Errors = new[] { "Unknown error" } };

            return await response.Content.ReadFromJsonAsync<CreateUserResultDto>()
                   ?? new CreateUserResultDto { Succeeded = false, Errors = new[] { "Failed to create user" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user");
            return new CreateUserResultDto { Succeeded = false, Errors = new[] { ex.Message } };
        }
    }

    public async Task<IEnumerable<CompanySelectItemDto>> GetActiveCompaniesForSelectAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<CompanySelectItemDto>>(
                "api/v1/internal/admin/companies-for-select") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get companies for select");
            return new List<CompanySelectItemDto>();
        }
    }

    // ── Helper DTOs for JSON deserialization ──────────────────────────────
    private record ErrorResponse(string? Error);
    private record ChangeCompanyResult(string? CompanyName);
    private record ToggleResult(string? Status);
    private record CreateTenantResult(Guid CompanyId);
}
