using Horizon.Notifications.Application.UseCases;

namespace Horizon.Notifications.Adapters.Inbound.Messaging.Contracts;

// Evento publicado por Horizon.Identity cuando alguien pide recuperar su contraseña
public sealed record PasswordResetRequested(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string ResetUrl,
    DateTime ExpiresAt)
{
    public const string RoutingKey = "identity.password-reset-requested";

    public PasswordResetRequest ToRequest() =>
        new(EventId, OccurredAt, UserId, Email, ResetUrl, ExpiresAt);
}