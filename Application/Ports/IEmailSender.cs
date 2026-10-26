namespace Horizon.Notifications.Application.Ports;

// un correo listo para enviar: a quién, el asunto y el contenido en HTML
public sealed record EmailMessage(string To, string Subject, string HtmlBody);

// Lo que el centro necesita para enviar correos, sin saber si es Azure, SMTP u otro
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}