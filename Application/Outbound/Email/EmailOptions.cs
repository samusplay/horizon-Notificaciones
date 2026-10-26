using System.ComponentModel.DataAnnotations;

namespace Horizon.Notifications.Adapters.Outbound.Email;

public sealed class EmailOptions
{
    public const string Section = "Email";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    // el remitente que te da Azure, ej: DoNotReply@xxxx.azurecomm.net
    [Required, EmailAddress]
    public string SenderAddress { get; init; } = string.Empty;
}