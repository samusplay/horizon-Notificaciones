using Horizon.Notifications.Domain;

namespace Horizon.Notifications.Application.Ports;

// Lo que el centro necesita para guardar el historial, sin saber que es Cassandra
public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct);

    // las más recientes primero, como máximo `limit`
    Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, int limit, CancellationToken ct);

    // guarda los cambios de una notificación existente (por ejemplo, marcada como leída)
    Task UpdateAsync(Notification notification, CancellationToken ct);
}