using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace WebApp.Publishers;

public class MonolithEventPublisher
{
    private readonly RabbitMqPublisher _publisher;
    private const string Exchange = "monolith.events";

    public MonolithEventPublisher(RabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task PublishAuditLogRequestAsync(AuditLogRequestMessage message)
    {
        await _publisher.PublishAsync(Exchange, "audit.log.request", message);
    }
}
