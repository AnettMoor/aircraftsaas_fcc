using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace WebApp.Consumers;

/// <summary>
/// Consumes user registration events from the Users microservice.
/// Can be used for cache invalidation, local logging, or other side effects.
/// </summary>
public class UserRegisteredConsumer : RabbitMqConsumerBase<UserRegisteredMessage>
{
    public UserRegisteredConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<UserRegisteredConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "users.events",
            queue: "monolith.user.registered",
            routingKey: "user.registered")
    { }

    protected override Task HandleMessageAsync(
        UserRegisteredMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var logger = sp.GetRequiredService<ILogger<UserRegisteredConsumer>>();
        logger.LogInformation(
            "User registered event received: {UserId} ({Email})",
            message.UserId, message.Email);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumes company created events from the Users microservice.
/// </summary>
public class CompanyCreatedConsumer : RabbitMqConsumerBase<CompanyCreatedMessage>
{
    public CompanyCreatedConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<CompanyCreatedConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "users.events",
            queue: "monolith.company.created",
            routingKey: "company.created")
    { }

    protected override Task HandleMessageAsync(
        CompanyCreatedMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var logger = sp.GetRequiredService<ILogger<CompanyCreatedConsumer>>();
        logger.LogInformation(
            "Company created event received: {CompanyId} ({CompanyName})",
            message.CompanyId, message.CompanyName);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumes company updated events from the Users microservice.
/// </summary>
public class CompanyUpdatedConsumer : RabbitMqConsumerBase<CompanyUpdatedMessage>
{
    public CompanyUpdatedConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<CompanyUpdatedConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "users.events",
            queue: "monolith.company.updated",
            routingKey: "company.updated")
    { }

    protected override Task HandleMessageAsync(
        CompanyUpdatedMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var logger = sp.GetRequiredService<ILogger<CompanyUpdatedConsumer>>();
        logger.LogInformation(
            "Company updated event received: {CompanyId} ({CompanyName}), Active={IsActive}",
            message.CompanyId, message.CompanyName, message.IsActive);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumes user company changed events from the Users microservice.
/// </summary>
public class UserCompanyChangedConsumer : RabbitMqConsumerBase<UserCompanyChangedMessage>
{
    public UserCompanyChangedConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<UserCompanyChangedConsumer> logger)
        : base(connection, scopeFactory, logger,
            exchange: "users.events",
            queue: "monolith.user.company.changed",
            routingKey: "user.company.changed")
    { }

    protected override Task HandleMessageAsync(
        UserCompanyChangedMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var logger = sp.GetRequiredService<ILogger<UserCompanyChangedConsumer>>();
        logger.LogInformation(
            "User company changed: {UserId} from {OldCompanyId} to {NewCompanyId}",
            message.UserId, message.OldCompanyId, message.NewCompanyId);
        return Task.CompletedTask;
    }
}
