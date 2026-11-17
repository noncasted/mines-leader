using Common.Extensions;
using Npgsql;

namespace Game.MatchMetrics;

// The tables of the collector, created by the deploy setup of a game (Create, each table only when it is missing). Append
// only: a batch adds its chunks and events and moves the totals and the last seq of its stream.
// - series: the dictionary of (name, tags), global for the game.
// - streams: one writer in one match; user_id is the meta id, Guid.Empty for the stream of the server.
// - slots: the slot of a series in its stream, as the client names it in its chunks (kept for a reconnect to another gateway).
// - chunks: the values of a series in buckets of a second of the clock of the run from start; at - the server time of start.
// - totals: per stream and series the sum, the count and the max of the values (NaN of a gauge not counted).
// - events: the rare events with their fields.
public static class MatchMetricsSchema
{
    public const string Series = "match_metric_series";
    public const string Streams = "match_metric_streams";
    public const string Slots = "match_metric_slots";
    public const string Chunks = "match_metric_chunks";
    public const string Totals = "match_metric_totals";
    public const string Events = "match_metric_events";

    public static readonly IReadOnlyList<(string Table, string Sql)> Tables = new[]
    {
        (Series, $"""
                  CREATE TABLE {Series} (
                      id int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                      name text NOT NULL,
                      kind smallint NOT NULL,
                      tags jsonb NOT NULL,
                      UNIQUE (name, tags)
                  );
                  """),
        (Streams, $"""
                   CREATE TABLE {Streams} (
                       id uuid PRIMARY KEY,
                       match_id uuid NOT NULL,
                       user_id uuid NOT NULL,
                       writer_id uuid NOT NULL,
                       kind smallint NOT NULL,
                       opened_at timestamptz NOT NULL,
                       last_seq int NOT NULL,
                       last_at timestamptz NOT NULL,
                       UNIQUE (match_id, user_id, writer_id)
                   );
                   CREATE INDEX ix_{Streams}_user ON {Streams} (user_id, opened_at DESC);
                   """),
        (Slots, $"""
                 CREATE TABLE {Slots} (
                     stream_id uuid NOT NULL,
                     slot smallint NOT NULL,
                     series_id int NOT NULL,
                     PRIMARY KEY (stream_id, slot)
                 );
                 """),
        (Chunks, $"""
                  CREATE TABLE {Chunks} (
                      stream_id uuid NOT NULL,
                      seq int NOT NULL,
                      series_id int NOT NULL,
                      start int NOT NULL,
                      at timestamptz NOT NULL,
                      values real[] NOT NULL,
                      PRIMARY KEY (stream_id, seq, series_id, start)
                  );
                  """),
        (Totals, $"""
                  CREATE TABLE {Totals} (
                      stream_id uuid NOT NULL,
                      series_id int NOT NULL,
                      sum double precision NOT NULL,
                      count int NOT NULL,
                      max real NOT NULL,
                      PRIMARY KEY (stream_id, series_id)
                  );
                  """),
        (Events, $"""
                  CREATE TABLE {Events} (
                      stream_id uuid NOT NULL,
                      seq int NOT NULL,
                      idx smallint NOT NULL,
                      time real NOT NULL,
                      at timestamptz NOT NULL,
                      name text NOT NULL,
                      fields jsonb NOT NULL,
                      PRIMARY KEY (stream_id, seq, idx)
                  );
                  CREATE INDEX ix_{Events}_name ON {Events} (name);
                  """)
    };

    public static async Task Create(NpgsqlConnection connection)
    {
        foreach (var (table, sql) in Tables)
            await connection.CreateIfNotExists(table, sql);
    }
}
