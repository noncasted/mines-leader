using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Aspire;

public static class BenchmarkSetup {
    public static async Task Run(IConfigurationManager configuration) {
        await using var connection = await configuration.GetConnection();

        var tableName = DbLookup.Benchmark_Results;

        var createSql = @$"
            CREATE TABLE {tableName} (
                id uuid PRIMARY KEY,
                benchmark_name character varying(256) NOT NULL,
                ""group"" character varying(128) NOT NULL,
                metric_name character varying(128) NOT NULL,
                metric_value double precision NOT NULL,
                duration_ms bigint NOT NULL,
                payload_json jsonb NOT NULL DEFAULT '{"{}"}'::jsonb,
                timestamp timestamptz NOT NULL DEFAULT now(),
                success boolean NOT NULL DEFAULT true,
                error_message text NOT NULL DEFAULT ''
            );

            CREATE INDEX ix_{tableName}_name_ts
                ON {tableName} (benchmark_name, timestamp DESC);
            ";

        await connection.CreateIfNotExists(tableName, createSql);

        var snapshotsTable = DbLookup.Benchmark_Snapshots;

        var snapshotsSql = @$"
            CREATE TABLE {snapshotsTable} (
                result_id uuid NOT NULL REFERENCES {tableName}(id) ON DELETE CASCADE,
                step_index integer NOT NULL,
                step_percent real NOT NULL,
                metric_value double precision NOT NULL,
                diff_value double precision NOT NULL,
                PRIMARY KEY (result_id, step_index)
            );

            CREATE INDEX ix_{snapshotsTable}_result
                ON {snapshotsTable} (result_id);
            ";

        await connection.CreateIfNotExists(snapshotsTable, snapshotsSql);
    }
}
