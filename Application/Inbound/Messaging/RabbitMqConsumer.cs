using System.Text.Json;
using Horizon.Notifications.Adapters.Inbound.Messaging.Contracts;
using Horizon.Notifications.Application.UseCases;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Horizon.Notifications.Adapters.Inbound.Messaging;

// Adaptador de ENTRADA: escucha RabbitMQ y dispara los casos de uso
internal sealed class RabbitMqConsumer : BackgroundService
{
    private const string Exchange = "horizon.events";
    private const string Queue = "notifications.email";
    private const int MaxDeliveries = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumer> _logger;

    public RabbitMqConsumer(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.User,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await DeclareTopologyAsync(channel, stoppingToken);

        // de a UN mensaje a la vez: no pide el siguiente hasta responder ack/nack
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnMessageAsync(channel, delivery, stoppingToken);

        // autoAck: false → NOSOTROS decidimos cuándo confirmar
        await channel.BasicConsumeAsync(queue: Queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        _logger.LogInformation("Escuchando la cola {Queue}", Queue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken); // vive mientras viva la app
        }
        catch (OperationCanceledException)
        {
            // la app se está apagando
        }
    }

    // Exchange, cola y bindings (como CREATE TABLE IF NOT EXISTS: idempotente)
    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(exchange: Exchange, type: ExchangeType.Topic, durable: true, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",          // cola replicada y que cuenta las entregas
                ["x-delivery-limit"] = MaxDeliveries  // al 5.º fallo se descarta (sin bucle infinito)
            },
            cancellationToken: ct);

        await channel.QueueBindAsync(Queue, Exchange, ChannelConfirmationRequested.RoutingKey, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, Exchange, PasswordResetRequested.RoutingKey, cancellationToken: ct);
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        try
        {
            // un scope por mensaje (como una petición HTTP)
            await using var scope = _scopeFactory.CreateAsyncScope();
            await DispatchAsync(delivery.RoutingKey, delivery.Body, scope.ServiceProvider, ct);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is JsonException or InvalidMessageException)
        {
            // venenoso: reintentar no sirve → se descarta (NO se registra el cuerpo: lleva el token)
            _logger.LogError(ex, "Mensaje inválido descartado. RoutingKey: {RoutingKey}", delivery.RoutingKey);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // pasajero (Azure 429, Cassandra timeout...): esperar y devolver a la cola
            _logger.LogWarning(ex, "Fallo procesando {RoutingKey}; se reintentará", delivery.RoutingKey);
            await Task.Delay(RetryDelay, ct);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, ct);
        }
    }

    // routing key → contrato → caso de uso
    private static async Task DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct)
    {
        switch (routingKey)
        {
            case ChannelConfirmationRequested.RoutingKey:
                {
                    var message = Deserialize<ChannelConfirmationRequested>(body);
                    Require(message.Email, message.ChannelName, message.ConfirmationUrl);
                    await services.GetRequiredService<SendChannelConfirmation>().ExecuteAsync(message.ToRequest(), ct);
                    break;
                }
            case PasswordResetRequested.RoutingKey:
                {
                    var message = Deserialize<PasswordResetRequested>(body);
                    Require(message.Email, message.ResetUrl);
                    await services.GetRequiredService<SendPasswordReset>().ExecuteAsync(message.ToRequest(), ct);
                    break;
                }
            default:
                throw new InvalidMessageException($"Routing key desconocida: {routingKey}");
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span, Json)
        ?? throw new InvalidMessageException("Mensaje vacío");

    private static void Require(params string?[] values)
    {
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidMessageException("Faltan campos obligatorios");
    }

    private sealed class InvalidMessageException(string message) : Exception(message);
}