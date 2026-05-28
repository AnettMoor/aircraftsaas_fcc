using System.Net.Http.Json;
using Users.Application.DTOs;
using Users.Application.Interfaces;

namespace WebApp.Proxies;

/// <summary>
/// Minimal HTTP proxy for ICompanyService — only implements methods needed by
/// the monolith middleware (e.g. IsCompanyActiveAsync for TenantResolutionMiddleware).
/// Full company CRUD goes through the Users service directly.
/// </summary>
public class CompanyServiceProxy : ICompanyService
{
    private readonly HttpClient _http;
    private readonly ILogger<CompanyServiceProxy> _logger;

    public CompanyServiceProxy(HttpClient http, ILogger<CompanyServiceProxy> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> IsCompanyActiveAsync(Guid companyId)
    {
        try
        {
            return await _http.GetFromJsonAsync<bool>(
                $"api/v1/internal/tenant/company-active/{companyId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if company {CompanyId} is active", companyId);
            return false;
        }
    }

    // ── The following methods are not used by the monolith middleware ──────
    // They throw NotSupportedException because company CRUD should go through
    // the Users service directly, not through the monolith proxy.

    public Task<CompanyDto?> GetByIdAsync(Guid id)
        => throw new NotSupportedException("Company CRUD should go through Users service directly.");

    public Task<CompanyDto?> GetBySlugAsync(string slug)
        => throw new NotSupportedException("Company CRUD should go through Users service directly.");

    public Task<IEnumerable<CompanyDto>> GetAllAsync()
        => throw new NotSupportedException("Company CRUD should go through Users service directly.");

    public Task<CompanyDto> CreateAsync(CreateCompanyDto dto, string createdBy)
        => throw new NotSupportedException("Company CRUD should go through Users service directly.");

    public Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyDto dto, string updatedBy, Guid callerId, bool isAdmin = false)
        => throw new NotSupportedException("Company CRUD should go through Users service directly.");

    public Task DeleteAsync(Guid id, string deletedBy, Guid callerId, bool isAdmin = false)
        => throw new NotSupportedException("Company CRUD should go through Users service directly.");
}
