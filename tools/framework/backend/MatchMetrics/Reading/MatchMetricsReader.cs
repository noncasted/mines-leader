using System.Text.Json;
using Common.Extensions;
using Npgsql;
using Shared;
using static Game.MatchMetrics.MatchMetricsSchema;

namespace Game.MatchMetrics;

// The queries over the tables of the collector, every value of a query a parameter. The points are summed in memory
// (MatchMetricPointsAggregation), the totals in SQL: they are read across matches.
public sealed class MatchMetricsReader : IMatchMetricsReader
{
    public MatchMetricsReader(IDbSource db)
    {
        _db = db;
    }

    private readonly IDbSource _db;

    public Task<IReadOnlyList<MatchMetricStreamInfo>> GetStreams(Guid matchId)
    {
        return Read(
            $"SELECT id, match_id, user_id, writer_id, kind, opened_at, last_seq, last_at FROM {Streams} WHERE match_id = @match ORDER BY kind, opened_at",
            command => command.Parameters.AddWithValue("match", matchId),
            reader => new MatchMetricStreamInfo(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                (SharedMatchMetrics.MatchStreamKind)reader.GetInt16(4),
                reader.GetDateTime(5),
                reader.GetInt32(6),
                reader.GetDateTime(7)));
    }

    public Task<IReadOnlyList<MatchMetricSeriesInfo>> GetSeries(string? name)
    {
        MatchMetricQuerySql.CheckName(name);

        return Read(
            $"SELECT id, name, kind, tags::text FROM {Series} WHERE @name::text IS NULL OR name = @name ORDER BY name, id",
            command => command.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value),
            reader => new MatchMetricSeriesInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                (SharedMatchMetrics.MatchSeriesKind)reader.GetInt16(2),
                Tags(reader.GetString(3))));
    }

    public async Task<IReadOnlyList<MatchMetricGroupPoints>> GetPoints(Guid matchId, MatchMetricPointsQuery query)
    {
        var filter = query.Filter;
        var rows = await Read(
            command =>
            {
                command.Parameters.AddWithValue("match", matchId);
                var where = MatchMetricQuerySql.Where(command, filter);
                var group = MatchMetricQuerySql.GroupColumns(command, filter);

                return $"""
                        SELECT s.id, t.id, s.name, s.kind, c.start, c.at, c.values{group}
                        FROM {Chunks} c JOIN {Series} s ON s.id = c.series_id JOIN {Streams} t ON t.id = c.stream_id
                        WHERE t.match_id = @match{where}
                        """;
            },
            reader => new MatchMetricPointRow(
                reader.GetInt32(0),
                reader.GetGuid(1),
                reader.GetString(2),
                (SharedMatchMetrics.MatchSeriesKind)reader.GetInt16(3),
                MatchMetricQuerySql.ReadGroup(reader, filter, 7),
                reader.GetInt32(4),
                reader.GetDateTime(5),
                reader.GetFieldValue<float[]>(6)));

        return MatchMetricPointsAggregation.Of(rows, query);
    }

    public Task<IReadOnlyList<MatchMetricGroupTotal>> GetTotals(MatchMetricTotalsQuery query)
    {
        var filter = query.Filter;
        var keys = string.Concat(Enumerable.Range(0, filter.GroupBy.Count).Select(i => $", g{i}"));

        return Read(
            command =>
            {
                var where = MatchMetricQuerySql.Where(command, filter);
                var group = MatchMetricQuerySql.GroupColumns(command, filter);
                command.Parameters.AddWithValue("match", (object?)query.MatchId ?? DBNull.Value);
                command.Parameters.AddWithValue("since", (object?)query.Since ?? DBNull.Value);
                command.Parameters.AddWithValue("until", (object?)query.Until ?? DBNull.Value);

                return $"""
                        SELECT name, min(kind), sum(sum), sum(count), max(max), count(DISTINCT stream), count(DISTINCT kind){keys}
                        FROM (
                            SELECT s.name, s.kind, o.sum, o.count, o.max, o.stream_id AS stream{group}
                            FROM {Totals} o JOIN {Series} s ON s.id = o.series_id JOIN {Streams} t ON t.id = o.stream_id
                            WHERE (@match::uuid IS NULL OR t.match_id = @match)
                              AND (@since::timestamptz IS NULL OR t.opened_at >= @since)
                              AND (@until::timestamptz IS NULL OR t.opened_at <= @until){where}
                        ) rows
                        GROUP BY name{keys}
                        ORDER BY name{keys}
                        """;
            },
            reader =>
            {
                if (reader.GetInt64(6) > 1)
                    throw new MatchMetricsQueryException($"The group of {reader.GetString(0)} has counters and gauges: group by more tags");

                return new MatchMetricGroupTotal(
                    reader.GetString(0),
                    MatchMetricQuerySql.ReadGroup(reader, filter, 7),
                    (SharedMatchMetrics.MatchSeriesKind)reader.GetInt16(1),
                    reader.GetDouble(2),
                    reader.GetInt64(3),
                    reader.GetFloat(4),
                    (int)reader.GetInt64(5));
            });
    }

    public Task<IReadOnlyList<MatchMetricEventInfo>> GetEvents(Guid matchId, MatchMetricEventsQuery query)
    {
        MatchMetricQuerySql.CheckName(query.Name);

        return Read(
            $"""
             SELECT e.stream_id, t.user_id, t.kind, e.seq, e.idx, e.time, e.at, e.name, e.fields::text
             FROM {Events} e JOIN {Streams} t ON t.id = e.stream_id
             WHERE t.match_id = @match
               AND (@name::text IS NULL OR e.name = @name)
               AND (@user::uuid IS NULL OR t.user_id = @user)
               AND (@from::real IS NULL OR e.time >= @from)
               AND (@to::real IS NULL OR e.time <= @to)
             ORDER BY e.time, t.kind, t.user_id, e.seq, e.idx
             """,
            command =>
            {
                command.Parameters.AddWithValue("match", matchId);
                command.Parameters.AddWithValue("name", (object?)query.Name ?? DBNull.Value);
                command.Parameters.AddWithValue("user", (object?)query.UserId ?? DBNull.Value);
                command.Parameters.AddWithValue("from", (object?)query.From ?? DBNull.Value);
                command.Parameters.AddWithValue("to", (object?)query.To ?? DBNull.Value);
            },
            reader => new MatchMetricEventInfo(
                reader.GetGuid(0),
                reader.GetGuid(1),
                (SharedMatchMetrics.MatchStreamKind)reader.GetInt16(2),
                reader.GetInt32(3),
                reader.GetInt16(4),
                reader.GetFloat(5),
                reader.GetDateTime(6),
                reader.GetString(7),
                Fields(reader.GetString(8))));
    }

    private static JsonElement Fields(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private Task<IReadOnlyList<T>> Read<T>(string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> row)
    {
        return Read(command =>
        {
            parameters(command);
            return sql;
        }, row);
    }

    private async Task<IReadOnlyList<T>> Read<T>(Func<NpgsqlCommand, string> build, Func<NpgsqlDataReader, T> row)
    {
        await using var connection = await _db.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = build(command);

        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();

        while (await reader.ReadAsync())
            rows.Add(row(reader));

        return rows;
    }

    private static IReadOnlyDictionary<string, string> Tags(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? throw new InvalidOperationException($"[MatchMetrics] Tags {json}");
    }
}
