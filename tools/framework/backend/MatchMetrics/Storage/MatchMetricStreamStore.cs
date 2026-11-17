using Common.Extensions;
using Shared;

namespace Game.MatchMetrics;

// Opens a stream by its writer: the first Open creates the row, an Open after a reconnect (to any gateway) finds the same
// row with its last seq and slots.
internal sealed class MatchMetricStreamStore
{
    public MatchMetricStreamStore(IDbSource db)
    {
        _db = db;
    }

    private readonly IDbSource _db;

    public async Task<MatchMetricStream> Open(Guid matchId, Guid userId, Guid writerId, SharedMatchMetrics.MatchStreamKind kind, DateTime now)
    {
        await using var connection = await _db.OpenConnection();

        Guid id;
        int lastSeq;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                                   INSERT INTO {MatchMetricsSchema.Streams} (id, match_id, user_id, writer_id, kind, opened_at, last_seq, last_at)
                                   VALUES (@id, @match, @user, @writer, @kind, @now, 0, @now)
                                   ON CONFLICT (match_id, user_id, writer_id) DO UPDATE SET match_id = EXCLUDED.match_id
                                   RETURNING id, last_seq
                                   """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("match", matchId);
            command.Parameters.AddWithValue("user", userId);
            command.Parameters.AddWithValue("writer", writerId);
            command.Parameters.AddWithValue("kind", (short)kind);
            command.Parameters.AddWithValue("now", now);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync() == false)
                throw new InvalidOperationException($"[MatchMetrics] No stream for the writer {writerId} of the match {matchId}");

            id = reader.GetGuid(0);
            lastSeq = reader.GetInt32(1);
        }

        var slots = await MatchMetricSlots.Read(connection, id);

        return new MatchMetricStream(id, matchId, lastSeq, slots);
    }
}
