namespace Horizon.Notifications.Adapters.Outbound.Persistence;

// Crea el keyspace y la tabla si no existen (cumple el papel de las migraciones)
internal static class CassandraSchema
{
    public static void Ensure(Cassandra.ISession session, string keyspace)
    {
        // el keyspace es como "la base de datos": define cuántas copias se guardan
        session.Execute($$"""
            CREATE KEYSPACE IF NOT EXISTS {{keyspace}}
            WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1}
            """);

        session.ChangeKeyspace(keyspace);

        // tabla diseñada para: "las últimas notificaciones de un usuario"
        session.Execute("""
            CREATE TABLE IF NOT EXISTS notifications_by_user (
                user_id    uuid,
                created_at timestamp,
                id         uuid,
                type       text,
                title      text,
                message    text,
                read_at    timestamp,
                PRIMARY KEY ((user_id), created_at, id)
            ) WITH CLUSTERING ORDER BY (created_at DESC, id ASC)
            """);
    }
}