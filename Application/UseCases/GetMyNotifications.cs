using Horizon.Notifications.Application.Ports;

namespace Horizon.Notifications.Application.UseCases;

// lo que ve el frontend de cada notificación (no la entidad del dominio)
public sealed record NotificationView(
    Guid Id,
    string Type,
    string Title,
    string Message,
    DateTime CreatedAt,
    bool IsRead);

public sealed record MyNotificationsResult(
    IReadOnlyList<NotificationView> Items,
    int UnreadCount);   // para el numerito rojo de la campana

public sealed class GetMyNotifications(INotificationRepository notifications)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<MyNotificationsResult> ExecuteAsync(Guid userId, int? limit, CancellationToken ct)
    {
        // el caso de uso se protege solo: nunca menos de 1 ni más de 50
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var items = await notifications.GetByUserAsync(userId, take, ct);

        var views = items
            .Select(n => new NotificationView(
                n.Id, n.Type.ToString(), n.Title, n.Message, n.CreatedAt, n.IsRead))
            .ToList();

        return new MyNotificationsResult(views, views.Count(v => !v.IsRead));
    }
}