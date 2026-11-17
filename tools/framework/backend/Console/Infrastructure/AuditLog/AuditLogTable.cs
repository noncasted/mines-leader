using Common.Extensions;
using Npgsql;

namespace Console;

// The table of AuditLogStorage. The deploy setup of a game creates it before the console starts.
public static class AuditLogTable
{
    public const string Name = "audit_log";

    private const string _createSql = $"""
        CREATE TABLE {Name} (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            timestamp timestamptz NOT NULL DEFAULT now(),
            action text NOT NULL,
            details text NOT NULL DEFAULT '',
            PRIMARY KEY (id)
        );
        CREATE INDEX ix_audit_log_timestamp ON {Name} USING btree (timestamp DESC);
        """;

    public static Task Create(NpgsqlConnection connection)
    {
        return connection.CreateIfNotExists(Name, _createSql);
    }
}
