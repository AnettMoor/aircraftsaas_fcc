using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Shared.Messaging;

public abstract class RabbitMqConsumerBase<TMessage> : BackgroundService
{
    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly string _exchange;
    private readonly string _queue;
    private readonly string _routingKey;

    protected RabbitMqConsumerBase(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        string exchange,
        string queue,
        string routingKey)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _exchange = exchange;
        _queue = queue;
        _routingKey = routingKey;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.GetChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: _exchange,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: _queue,
            exchange: _exchange,
            routingKey: _routingKey,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<TMessage>(json)!;

                using var scope = _scopeFactory.CreateScope();
                await HandleMessageAsync(message, scope.ServiceProvider, stoppingToken);

                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                _logger.LogInformation("Processed {MessageType} from {Queue}",
                    typeof(TMessage).Name, _queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {MessageType} from {Queue}",
                    typeof(TMessage).Name, _queue);
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: _queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    protected abstract Task HandleMessageAsync(
        TMessage message, IServiceProvider serviceProvider, CancellationToken ct);
}
