using Shared.Messaging;
using Shared.Messaging.Contracts;
using Users.Application.Interfaces;

namespace Users.WebHost.Consumers;

public class AuditLogRequestConsumer : RabbitMqConsumerBase<AuditLogRequestMessage>
{
    public AuditLogRequestConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogRequestConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "monolith.events",
            queue: "users.audit.log.request",
            routingKey: "audit.log.request")
    { }

    protected override async Task HandleMessageAsync(
        AuditLogRequestMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var auditService = sp.GetRequiredService<IAuditService>();

        await auditService.LogRequestAuditAsync(new Users.Application.DTOs.AuditRequestDto
        {
            TenantId = message.TenantId,
            UserId = message.UserId,
            EntityName = message.EntityName,
            EntityId = message.EntityId,
            Action = message.Action,
            OldValues = message.OldValues,
            NewValues = message.NewValues,
            IpAddress = message.IpAddress,
            Details = message.Details
        });
    }
}
