namespace App.Application.DTOs;

/// <summary>
/// DTO for audit log requests from middleware — decouples the middleware from domain entities.
/// </summary>
public class AuditRequestDto
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string EntityName { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = default!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string IpAddress { get; set; } = default!;
    public string? Details { get; set; }
}
