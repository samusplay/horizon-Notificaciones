namespace Horizon.Notifications.Domain;

// los tipos de notificación que existen hoy
public enum NotificationType { ChannelConfirmation, PasswordReset }

// Una notificación que el usuario ve en su campana.
// Regla de diseño: NUNCA contiene enlaces ni tokens (esos solo viajan en el correo).
public sealed class Notification
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public NotificationType Type { get; }
    public string Title { get; }
    public string Message { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    // se usa para crearla y también para reconstruirla al leerla de Cassandra
    public Notification(Guid id, Guid userId, NotificationType type,
        string title, string message, DateTime createdAt, DateTime? readAt)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Title = title;
        Message = message;
        CreatedAt = createdAt;
        ReadAt = readAt;
    }

    // ---------- una "fábrica" por tipo: el texto vive aquí, en un solo lugar ----------

    // el id y la fecha vienen del evento: si el mensaje se reprocesa, se genera la MISMA notificación
    public static Notification ChannelConfirmation(Guid id, Guid userId, string channelName, DateTime createdAt) =>
        new(id, userId, NotificationType.ChannelConfirmation,
            "Confirma tu canal",
            $"Te enviamos un correo para confirmar tu canal \"{channelName}\".",
            createdAt, readAt: null);

    public static Notification PasswordReset(Guid id, Guid userId, DateTime createdAt) =>
        new(id, userId, NotificationType.PasswordReset,
            "Recuperación de contraseña",
            "Te enviamos un correo con un enlace para restablecer tu contraseña.",
            createdAt, readAt: null);
    // marcar como leída dos veces no cambia la fecha de la primera lectura
    public void MarkAsRead(DateTime nowUtc)
    {
        ReadAt ??= nowUtc;
    }
}