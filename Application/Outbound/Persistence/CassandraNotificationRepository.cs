using Cassandra;
using Horizon.Notifications.Application.Ports;
using Horizon.Notifications.Domain;

namespace Horizon.Notifications.Adapters.Outbound.Persistence;

internal sealed class CassandraNotificationRepository : INotificationRepository
{
    private readonly Cassandra.ISession _session;
    private readonly PreparedStatement _insert;
    private readonly PreparedStatement _selectByUser;
    private readonly PreparedStatement _updateReadAt;

    public CassandraNotificationRepository(Cassandra.ISession session)
    {
        _session = session;

        // se preparan una sola vez; después solo se les pasan los valores
        _insert = session.Prepare("""
            INSERT INTO notifications_by_user (user_id, created_at, id, type, title, message, read_at)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """);

        _selectByUser = session.Prepare("""
            SELECT user_id, created_at, id, type, title, message, read_at
            FROM notifications_by_user
            WHERE user_id = ?
            LIMIT ?
            """);

        // para actualizar se necesita la clave primaria COMPLETA
        _updateReadAt = session.Prepare("""
            UPDATE notifications_by_user
            SET read_at = ?
            WHERE user_id = ? AND created_at = ? AND id = ?
            """);
    }

    public Task AddAsync(Notification notification, CancellationToken ct)
    {
        var statement = _insert.Bind(
                notification.UserId,
                ToTimestamp(notification.CreatedAt),
                notification.Id,
                notification.Type.ToString(),
                notification.Title,
                notification.Message,
                ToTimestampOrUnset(notification.ReadAt))
            .SetIdempotence(true);

        return _session.ExecuteAsync(statement).WaitAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, int limit, CancellationToken ct)
    {
        var rows = await _session.ExecuteAsync(_selectByUser.Bind(userId, limit)).WaitAsync(ct);

        // ya vienen ordenadas de la más nueva a la más vieja (CLUSTERING ORDER BY)
        return rows.Select(ToNotification).ToList();
    }

    // lo único que cambia de una notificación es read_at
    public Task UpdateAsync(Notification notification, CancellationToken ct)
    {
        var statement = _updateReadAt.Bind(
                ToTimestampOrUnset(notification.ReadAt),
                notification.UserId,
                ToTimestamp(notification.CreatedAt),
                notification.Id)
            .SetIdempotence(true);

        return _session.ExecuteAsync(statement).WaitAsync(ct);
    }

    private static Notification ToNotification(Row row) => new(
        id: row.GetValue<Guid>("id"),
        userId: row.GetValue<Guid>("user_id"),
        type: Enum.Parse<NotificationType>(row.GetValue<string>("type")),
        title: row.GetValue<string>("title"),
        message: row.GetValue<string>("message"),
        createdAt: row.GetValue<DateTimeOffset>("created_at").UtcDateTime,
        readAt: row.GetValue<DateTimeOffset?>("read_at")?.UtcDateTime);

    // Cassandra guarda los timestamp como DateTimeOffset en UTC
    private static DateTimeOffset ToTimestamp(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    // Unset = "no toques esta columna" (null crearía una lápida)
    private static object ToTimestampOrUnset(DateTime? utc) =>
        utc is null ? Unset.Value : ToTimestamp(utc.Value);
}