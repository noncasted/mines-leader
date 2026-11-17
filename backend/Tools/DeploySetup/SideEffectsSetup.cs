using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public static class SideEffectsSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        var queue = $@"
            CREATE TABLE {SideEffectTables.Queue} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_{SideEffectTables.Queue} ON {SideEffectTables.Queue} USING btree (created_at);
        ";

        var processing = $@"
            CREATE TABLE {SideEffectTables.Processing} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                processing_started_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
        ";

        var retry = $@"
            CREATE TABLE {SideEffectTables.RetryQueue} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                retry_after timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_{SideEffectTables.RetryQueue} ON {SideEffectTables.RetryQueue} USING btree (retry_after);
        ";

        var deadLetter = $@"
            CREATE TABLE {SideEffectTables.DeadLetter} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                failed_at timestamptz NOT NULL DEFAULT now(),
                error_message text,
                PRIMARY KEY (id)
            );
        ";

        await connection.CreateIfNotExists(SideEffectTables.Queue, queue);
        await connection.CreateIfNotExists(SideEffectTables.Processing, processing);
        await connection.CreateIfNotExists(SideEffectTables.RetryQueue, retry);
        await connection.CreateIfNotExists(SideEffectTables.DeadLetter, deadLetter);

    }
}