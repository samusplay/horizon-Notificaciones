using System.Net;
using Horizon.Notifications.Application.Ports;
using Horizon.Notifications.Domain;

namespace Horizon.Notifications.Application.UseCases;

// lo que llega en el evento PasswordResetRequested (lo publicará Identity)
public sealed record PasswordResetRequest(
    Guid EventId,          // id único del evento: se usa como id de la notificación
    DateTime OccurredAt,   // cuándo se pidió
    Guid UserId,
    string Email,
    string ResetUrl,
    DateTime ExpiresAt);   // hasta cuándo sirve el enlace (lo decide Identity)

public sealed class SendPasswordReset(
    IEmailSender emailSender,
    INotificationRepository notifications)
{
    public async Task ExecuteAsync(PasswordResetRequest request, CancellationToken ct)
    {
        // 1. historial primero, SIN el enlace (mismo id y fecha del evento: idempotente)
        var notification = Notification.PasswordReset(
            request.EventId, request.UserId, request.OccurredAt);
        await notifications.AddAsync(notification, ct);

        // 2. el correo con el enlace
        var email = new EmailMessage(
            To: request.Email,
            Subject: "Restablece tu contraseña de Horizon",
            HtmlBody: BuildBody(request));
        await emailSender.SendAsync(email, ct);
    }

    private static string BuildBody(PasswordResetRequest request)
    {
        var url = WebUtility.HtmlEncode(request.ResetUrl);

        // la duración la decide Identity; aquí solo la mostramos
        var minutes = (int)Math.Round((request.ExpiresAt - request.OccurredAt).TotalMinutes);

        return $"""
            <h2>Restablece tu contraseña</h2>
            <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta de Horizon.</p>
            <p><a href="{url}">Restablecer contraseña</a></p>
            <p>El enlace vence en {minutes} minutos y sirve una sola vez.
               Si no lo solicitaste, ignora este correo: tu contraseña no cambiará.</p>
            """;
    }
}