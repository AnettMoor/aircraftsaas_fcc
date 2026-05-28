using Users.Application.DTOs;

namespace Users.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditLogDto log);
    Task<IEnumerable<AuditLogDto>> GetLogsForTenantAsync(Guid tenantId, int page = 1, int pageSize = 50);
    Task<IEnumerable<AuditLogDto>> GetLogsForEntityAsync(Guid tenantId, string entityName, Guid entityId);
    
    /// <summary>
    /// Logs an audit entry from middleware without requiring direct DbContext access.
    /// </summary>
    Task LogRequestAuditAsync(AuditRequestDto auditRequest);
    
    /// <summary>
    /// Gets a JSON snapshot of an entity's current state for audit "old values" tracking.
    /// </summary>
    Task<string?> GetEntitySnapshotAsync(string entityName, Guid entityId);
}
