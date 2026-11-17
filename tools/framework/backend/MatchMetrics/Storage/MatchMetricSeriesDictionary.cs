using System.Collections.Concurrent;
using Common.Extensions;
using Npgsql;
using NpgsqlTypes;
using Shared;

namespace Game.MatchMetrics;

// The ids of the series of the game, cached for the life of the host: a row of the dictionary never changes or goes. Written
// outside the transaction of a batch, so a batch that fails leaves no id in the cache without its row. The same name and
// tags with another kind break the contract.
internal sealed class MatchMetricSeriesDictionary
{
    public MatchMetricSeriesDictionary(IDbSource db)
    {
        _db = db;
    }

    private readonly IDbSource _db;
    private readonly ConcurrentDictionary<MatchSeriesKey, MatchMetricSlot> _known = new();

    public async Task<MatchMetricSlot> Resolve(MatchSeriesKey key, SharedMatchMetrics.MatchSeriesKind kind)
    {
        if (_known.TryGetValue(key, out var known) == false)
        {
            known = await Upsert(key, kind);
            _known.TryAdd(key, known);
        }

        if (known.Kind != kind)
            throw new MatchMetricsRejectedException($"The series {key.Name} {key.Tags} is a {known.Kind}, not a {kind}");

        return known;
    }

    private async Task<MatchMetricSlot> Upsert(MatchSeriesKey key, SharedMatchMetrics.MatchSeriesKind kind)
    {
        await using var connection = await _db.OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
                               INSERT INTO {MatchMetricsSchema.Series} (name, kind, tags) VALUES (@name, @kind, @tags)
                               ON CONFLICT (name, tags) DO UPDATE SET name = EXCLUDED.name
                               RETURNING id, kind
                               """;
        command.Parameters.AddWithValue("name", key.Name);
        command.Parameters.AddWithValue("kind", (short)kind);
        command.Parameters.Add(new NpgsqlParameter("tags", NpgsqlDbType.Jsonb) { Value = key.Tags });

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync() == false)
            throw new InvalidOperationException($"[MatchMetrics] No id for the series {key.Name}");

        return new MatchMetricSlot(reader.GetInt32(0), (SharedMatchMetrics.MatchSeriesKind)reader.GetInt16(1));
    }
}
