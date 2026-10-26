using Horizon.Notifications.Application.UseCases;

namespace Horizon.Notifications.Adapters.Inbound.Messaging.Contracts;

// Evento publicado por Horizon.Channels al crear un canal o reenviar la confirmación
public sealed record ChannelConfirmationRequested(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string ChannelName,
    string ConfirmationUrl)
{
    public const string RoutingKey = "channels.confirmation-requested";

    // traduce: contrato externo → request del caso de uso
    public ChannelConfirmationRequest ToRequest() =>
        new(EventId, OccurredAt, UserId, Email, ChannelName, ConfirmationUrl);
}