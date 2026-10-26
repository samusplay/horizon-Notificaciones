namespace Horizon.Notifications.Adapters.Outbound.Persistence;

public sealed class CassandraOptions
{
    public const string Section = "Cassandra";

    public string ContactPoint { get; init; } = "localhost";
    public int Port { get; init; } = 9042;
    public string Keyspace { get; init; } = "horizon_notifications";
}