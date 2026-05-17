using App.Application.DTOs;
using App.Application.Interfaces;
using App.Domain.Contracts;
using App.Domain.DTOs;
using App.Domain.Entities;

namespace App.Application.Services;

public class AuditService : IAuditService
{
    private readonly IAppUOW _uow;
    private readonly ITenantService _tenantService;
    private readonly IRequestContextProvider _requestContextProvider;
    
    public AuditService(
        IAppUOW uow, 
        ITenantService tenantService,
        IRequestContextProvider requestContextProvider)
    {
        _uow = uow;
        _tenantService = tenantService;
        _requestContextProvider = requestContextProvider;
    }
    
    public async Task LogAsync(AuditLogDto log)
    {
        var auditLog = new AuditLog
        {
            TenantId = log.TenantId,
            UserId = log.UserId,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            Action = log.Action,
            OldValues = log.OldValues,
            NewValues = log.NewValues,
            Timestamp = DateTime.UtcNow,
            IpAddress = _requestContextProvider.GetClientIpAddress() ?? "Unknown",
            Details = log.Details
        };
        
        _uow.AuditLogRepository.Add(auditLog);
        await _uow.SaveChangesAsync();
    }
    
    public async Task LogRequestAuditAsync(AuditRequestDto auditRequest)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = auditRequest.TenantId ?? Guid.Empty,
            UserId = auditRequest.UserId,
            EntityName = auditRequest.EntityName,
            EntityId = auditRequest.EntityId,
            Action = auditRequest.Action,
            OldValues = auditRequest.OldValues,
            NewValues = auditRequest.NewValues,
            Timestamp = DateTime.UtcNow,
            IpAddress = auditRequest.IpAddress,
            Details = auditRequest.Details
        };
        
        _uow.AuditLogRepository.Add(auditLog);
        await _uow.SaveChangesAsync();
    }
    
    public async Task<string?> GetEntitySnapshotAsync(string entityName, Guid entityId)
    {
        return await _uow.AuditLogRepository.GetEntitySnapshotAsync(entityName, entityId);
    }
    
    public async Task<IEnumerable<AuditLogDto>> GetLogsForTenantAsync(Guid tenantId, int page = 1, int pageSize = 50)
    {
        var logs = await _uow.AuditLogRepository.GetForTenantAsync(tenantId, page, pageSize);
        return logs.Select(MapToDto);
    }
    
    public async Task<IEnumerable<AuditLogDto>> GetLogsForEntityAsync(Guid tenantId, string entityName, Guid entityId)
    {
        var logs = await _uow.AuditLogRepository.GetForEntityAsync(tenantId, entityName, entityId);
        return logs.Select(MapToDto);
    }
    
    private static AuditLogDto MapToDto(AuditLog a)
    {
        return new AuditLogDto
        {
            Id = a.Id,
            TenantId = a.TenantId,
            UserId = a.UserId,
            UserName = a.User != null ? a.User.Email : null,
            EntityName = a.EntityName,
            EntityId = a.EntityId,
            Action = a.Action,
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            Timestamp = a.Timestamp,
            IpAddress = a.IpAddress,
            Details = a.Details
        };
    }
}
