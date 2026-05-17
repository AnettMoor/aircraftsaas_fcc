using System.Text.Json;
using App.Domain.Contracts;
using App.Infrastructure.Mappers;
using App.Domain.Entities;
using Base.DAL.Contracts;
using Base.DAL.EF;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class AuditLogRepository : BaseRepository<AuditLog, AuditLog, AppDbContext>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext dbContext, IBaseMapper<AuditLog, AuditLog> mapper)
        : base(dbContext, mapper)
    {
    }

    public AuditLogRepository(AppDbContext dbContext)
        : base(dbContext, new BaseMapper<AuditLog>())
    {
    }

    public async Task<IEnumerable<AuditLog>> GetForTenantAsync(Guid tenantId, int page = 1, int pageSize = 50)
    {
        return await RepositoryDbSet
            .Include(a => a.User)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetForEntityAsync(Guid tenantId, string entityName, Guid entityId)
    {
        return await RepositoryDbSet
            .Include(a => a.User)
            .Where(a => a.TenantId == tenantId && a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<string?> GetEntitySnapshotAsync(string entityName, Guid entityId)
    {
        try
        {
            object? entity = entityName.ToLowerInvariant() switch
            {
                "aircraft" => await RepositoryDbContext.Aircraft.FindAsync(entityId),
                "booking" => await RepositoryDbContext.Bookings.FindAsync(entityId),
                "airport" => await RepositoryDbContext.Airports.FindAsync(entityId),
                "company" => await RepositoryDbContext.Companies.FindAsync(entityId),
                "maintenance" => await RepositoryDbContext.MaintenanceRecords.FindAsync(entityId),
                "maintenancerecord" => await RepositoryDbContext.MaintenanceRecords.FindAsync(entityId),
                "license" => await RepositoryDbContext.Licenses.FindAsync(entityId),
                "insurance" => await RepositoryDbContext.InsurancePolicies.FindAsync(entityId),
                "insurancepolicy" => await RepositoryDbContext.InsurancePolicies.FindAsync(entityId),
                "review" => await RepositoryDbContext.Reviews.FindAsync(entityId),
                "payment" => await RepositoryDbContext.Payments.FindAsync(entityId),
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

    // System-admin methods
    
    public async Task<IEnumerable<AuditLog>> GetAllSystemWideWithUserAsync(string? entity, string? action, Guid? tenantId)
    {
        var query = RepositoryDbSet
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(a => a.EntityName == entity);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetDistinctEntityNamesAsync()
    {
        return await RepositoryDbSet
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetDistinctActionsAsync()
    {
        return await RepositoryDbSet
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }
}
