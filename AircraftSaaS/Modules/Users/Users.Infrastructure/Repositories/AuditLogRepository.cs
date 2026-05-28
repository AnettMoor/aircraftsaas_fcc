using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.DAL;
using Users.Application.Contracts;
using Users.Domain.Entities;

namespace Users.Infrastructure.Repositories;

internal sealed class AuditLogRepository : BaseRepository<AuditLog, AuditLog, UsersDbContext>, IAuditLogRepository
{
    public AuditLogRepository(UsersDbContext dbContext, IBaseMapper<AuditLog, AuditLog> mapper)
        : base(dbContext, mapper)
    {
    }

    public AuditLogRepository(UsersDbContext dbContext)
        : base(dbContext, new BaseMapper<AuditLog>())
    {
    }

    public async Task<IEnumerable<AuditLog>> GetForTenantAsync(Guid tenantId, int page = 1, int pageSize = 50)
    {
        return await RepositoryDbSet
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetForEntityAsync(Guid tenantId, string entityName, Guid entityId)
    {
        return await RepositoryDbSet
            .Where(a => a.TenantId == tenantId && a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<string?> GetEntitySnapshotAsync(string entityName, Guid entityId)
    {
        try
        {
            // Only snapshot Users-module entities; cross-module entities are handled by their own modules
            object? entity = entityName.ToLowerInvariant() switch
            {
                "company" => await RepositoryDbContext.Companies.FindAsync(entityId),
                "license" => await RepositoryDbContext.Licenses.FindAsync(entityId),
                "person" => await RepositoryDbContext.Persons.FindAsync(entityId),
                "contact" => await RepositoryDbContext.Contacts.FindAsync(entityId),
                "contacttype" => await RepositoryDbContext.ContactTypes.FindAsync(entityId),
                "appusercompany" => await RepositoryDbContext.AppUserCompanies.FindAsync(entityId),
                "auditlog" => await RepositoryDbContext.AuditLogs.FindAsync(entityId),
                _ => null
            };

            if (entity == null)
                return null;

            // Use reflection to get all simple properties (skip navigation properties)
            var properties = entity.GetType().GetProperties()
                .Where(p => p.CanRead 
                            && !p.Name.Equals("Id") 
                            && !p.Name.Equals("CreatedAt") 
                            && !p.Name.Equals("UpdatedAt"))
                .ToList();

            var dict = new Dictionary<string, object?>();
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(entity);
                    // Skip navigation properties and complex types
                    if (value != null && !value.GetType().IsClass || value is string)
                    {
                        dict[prop.Name] = value;
                    }
                }
                catch
                {
                    // Skip properties that throw
                }
            }

            return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return null;
        }
    }
}
