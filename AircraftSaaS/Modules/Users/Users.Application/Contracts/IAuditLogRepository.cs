using Shared.Kernel.DAL;
using Users.Domain.Entities;

namespace Users.Application.Contracts;

public interface IAuditLogRepository : IBaseRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetForTenantAsync(Guid tenantId, int page = 1, int pageSize = 50);
    Task<IEnumerable<AuditLog>> GetForEntityAsync(Guid tenantId, string entityName, Guid entityId);
    
    /// <summary>
    /// Gets a JSON snapshot of an entity's current property values for audit logging.
    /// Only the infrastructure layer has access to DbContext to look up entities by type.
    /// </summary>
    Task<string?> GetEntitySnapshotAsync(string entityName, Guid entityId);
}
