using System.Net;
using Horizon.Notifications.Application.Ports;
using Horizon.Notifications.Domain;

namespace Horizon.Notifications.Application.UseCases;

// lo que llega en el evento ChannelConfirmationRequested (lo publicará Channels)
public sealed record ChannelConfirmationRequest(
    Guid EventId,          // id único del evento: se usa como id de la notificación
    DateTime OccurredAt,   // cuándo pasó: se usa como fecha de la notificación
    Guid UserId,
    string Email,
    string ChannelName,
    string ConfirmationUrl);

public sealed class SendChannelConfirmation(
    IEmailSender emailSender,
    INotificationRepository notifications)
{
    public async Task ExecuteAsync(ChannelConfirmationRequest request, CancellationToken ct)
    {
        // 1. guardamos el historial, SIN el enlace. Si el mensaje se reprocesa,
        //    el mismo id y la misma fecha sobrescriben la misma fila (no se duplica)
        var notification = Notification.ChannelConfirmation(
            request.EventId, request.UserId, request.ChannelName, request.OccurredAt);
        await notifications.AddAsync(notification, ct);

        // 2. enviamos el correo: el enlace (secreto) solo viaja aquí
        var email = new EmailMessage(
            To: request.Email,
            Subject: "Confirma tu canal en Horizon",
            HtmlBody: BuildBody(request));
        await emailSender.SendAsync(email, ct);
    }

    private static string BuildBody(ChannelConfirmationRequest request)
    {
        // el nombre lo escribió el usuario: se escapa para que no pueda inyectar HTML en el correo
        var channelName = WebUtility.HtmlEncode(request.ChannelName);
        var url = WebUtility.HtmlEncode(request.ConfirmationUrl);

        return $"""
            <h2>Confirma tu canal "{channelName}"</h2>
            <p>Para activar tu canal en Horizon, haz clic en el siguiente enlace:</p>
            <p><a href="{url}">Confirmar mi canal</a></p>
            <p>El enlace vence en 24 horas. Si no creaste este canal, ignora este correo.</p>
            """;
    }
}