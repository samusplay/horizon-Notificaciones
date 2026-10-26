using Azure;
using Azure.Communication.Email;
using Horizon.Notifications.Application.Ports;
using Microsoft.Extensions.Options;
using PortEmailMessage = Horizon.Notifications.Application.Ports.EmailMessage;

namespace Horizon.Notifications.Adapters.Outbound.Email;

internal sealed class AzureEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly EmailOptions _options;
    private readonly ILogger<AzureEmailSender> _logger;

    public AzureEmailSender(
        EmailClient client,
        IOptions<EmailOptions> options,
        ILogger<AzureEmailSender> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(PortEmailMessage message, CancellationToken ct)
    {
        // Started = esperar solo a que Azure ACEPTE el correo (no a que lo entregue)
        var operation = await _client.SendAsync(
            WaitUntil.Started,
            senderAddress: _options.SenderAddress,
            recipientAddress: message.To,
            subject: message.Subject,
            htmlContent: message.HtmlBody,
            cancellationToken: ct);

        // se registra el id de la operación, NO el correo del usuario
        _logger.LogInformation("Correo aceptado por Azure. OperationId: {OperationId}", operation.Id);
    }
}