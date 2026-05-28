namespace Shared.Messaging.Contracts;

public record AuditLogRequestMessage(
    Guid TenantId,
    Guid? UserId,
    string EntityName,
    Guid EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    string IpAddress,
    string? Details,
    DateTime Timestamp);
