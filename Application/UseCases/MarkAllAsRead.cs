using Horizon.Notifications.Application.Ports;

namespace Horizon.Notifications.Application.UseCases;

public sealed record MarkAllAsReadResult(int Marked); // cuántas se marcaron

public sealed class MarkAllAsRead(INotificationRepository notifications)
{
    public async Task<MarkAllAsReadResult> ExecuteAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // las mismas que puede ver el usuario en la campana (las últimas 50)
        var recent = await notifications.GetByUserAsync(userId, GetMyNotifications.MaxLimit, ct);
        var unread = recent.Where(n => !n.IsRead).ToList();

        foreach (var notification in unread)
        {
            notification.MarkAsRead(now);                        // la regla vive en la entidad
            await notifications.UpdateAsync(notification, ct);   // el repositorio solo guarda
        }

        return new MarkAllAsReadResult(unread.Count);
    }
}