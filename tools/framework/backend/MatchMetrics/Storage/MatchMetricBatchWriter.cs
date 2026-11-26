using Common.Extensions;
using Npgsql;
using NpgsqlTypes;

namespace Game.MatchMetrics;

// One batch - one transaction: the last seq of the stream moves only from seq - 1 (a copy is Duplicate, a hole is Gap, both
// write nothing), then the slots, the chunks and the events are added and the totals moved. The time of a chunk or an event
// on the server is its receive time minus its age.
internal sealed class MatchMetricBatchWriter
{
    public MatchMetricBatchWriter(IDbSource db, MatchMetricSeriesDictionary dictionary)
    {
        _db = db;
        _dictionary = dictionary;
    }

    private readonly IDbSource _db;
    private readonly MatchMetricSeriesDictionary _dictionary;

    public async Task<MatchMetricWrite> Write(MatchMetricStream stream, ParsedMatchBatch batch, DateTime receivedAt)
    {
        if (batch.Seq <= stream.LastSeq)
            return MatchMetricWrite.Duplicate;

        var added = new Dictionary<int, MatchMetricSlot>();

        foreach (var series in batch.Series)
            added.Add(series.Slot, await _dictionary.Resolve(series.Key, series.Kind));

        RequireNewSeries(stream, batch, added);

        await using var connection = await _db.OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync();

        if (await MoveSeq(connection, stream.Id, batch.Seq, receivedAt) == false)
        {
            await transaction.RollbackAsync();
            var lastSeq = await ReadLastSeq(connection, stream.Id);

            if (lastSeq < batch.Seq)
                return MatchMetricWrite.Gap;

            stream.OnDuplicate(lastSeq, await MatchMetricSlots.Read(connection, stream.Id));
            return MatchMetricWrite.Duplicate;
        }

        await InsertSlots(connection, stream.Id, added);
        await InsertChunks(connection, stream, batch, added, receivedAt);
        await InsertEvents(connection, stream.Id, batch, receivedAt);
        await UpsertTotals(connection, stream, batch, added);

        await transaction.CommitAsync();
        stream.OnStored(batch.Seq, added);

        return MatchMetricWrite.Stored;
    }

    // A series in one slot of a stream: the parser sees the keys of a batch only, the ids of the earlier slots are here.
    private static void RequireNewSeries(MatchMetricStream stream, ParsedMatchBatch batch, Dictionary<int, MatchMetricSlot> added)
    {
        foreach (var series in batch.Series)
        {
            var id = added[series.Slot].SeriesId;

            if (stream.Slots.Values.Any(slot => slot.SeriesId == id))
                throw new MatchMetricsRejectedException($"The series {series.Key.Name} {series.Key.Tags} is declared again in the slot {series.Slot}");
        }
    }

    private static async Task<bool> MoveSeq(NpgsqlConnection connection, Guid streamId, int seq, DateTime now)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {MatchMetricsSchema.Streams} SET last_seq = @seq, last_at = @now WHERE id = @id AND last_seq = @seq - 1";
        command.Parameters.AddWithValue("id", streamId);
        command.Parameters.AddWithValue("seq", seq);
        command.Parameters.AddWithValue("now", now);

        return await command.ExecuteNonQueryAsync() == 1;
    }

    private static async Task<int> ReadLastSeq(NpgsqlConnection connection, Guid streamId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT last_seq FROM {MatchMetricsSchema.Streams} WHERE id = @id";
        command.Parameters.AddWithValue("id", streamId);

        return (int)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"[MatchMetrics] No stream {streamId}"));
    }

    private static async Task InsertSlots(NpgsqlConnection connection, Guid streamId, Dictionary<int, MatchMetricSlot> added)
    {
        if (added.Count == 0)
            return;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
                               INSERT INTO {MatchMetricsSchema.Slots} (stream_id, slot, series_id)
                               SELECT @id, slot, series FROM unnest(@slots, @series) AS t(slot, series)
                               """;
        command.Parameters.AddWithValue("id", streamId);
        command.Parameters.AddWithValue("slots", added.Keys.Select(slot => (short)slot).ToArray());
        command.Parameters.AddWithValue("series", added.Values.Select(slot => slot.SeriesId).ToArray());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertChunks(
        NpgsqlConnection connection,
        MatchMetricStream stream,
        ParsedMatchBatch batch,
        Dictionary<int, MatchMetricSlot> added,
        DateTime receivedAt)
    {
        if (batch.Chunks.Count == 0)
            return;

        await using var import = await connection.BeginBinaryImportAsync(
            $"COPY {MatchMetricsSchema.Chunks} (stream_id, seq, series_id, start, at, values) FROM STDIN (FORMAT BINARY)");

        foreach (var chunk in batch.Chunks)
        {
            await import.StartRowAsync();
            await import.WriteAsync(stream.Id, NpgsqlDbType.Uuid);
            await import.WriteAsync(batch.Seq, NpgsqlDbType.Integer);
            await import.WriteAsync(SlotOf(stream, added, chunk.Slot).SeriesId, NpgsqlDbType.Integer);
            await import.WriteAsync(chunk.Start, NpgsqlDbType.Integer);
            await import.WriteAsync(receivedAt.AddMilliseconds(-chunk.AgeMs), NpgsqlDbType.TimestampTz);
            await import.WriteAsync(chunk.Values, NpgsqlDbType.Array | NpgsqlDbType.Real);
        }

        await import.CompleteAsync();
    }

    private static async Task InsertEvents(NpgsqlConnection connection, Guid streamId, ParsedMatchBatch batch, DateTime receivedAt)
    {
        if (batch.Events.Count == 0)
            return;

        await using var import = await connection.BeginBinaryImportAsync(
            $"COPY {MatchMetricsSchema.Events} (stream_id, seq, idx, time, at, name, fields) FROM STDIN (FORMAT BINARY)");

        for (var i = 0; i < batch.Events.Count; i++)
        {
            var entry = batch.Events[i];

            await import.StartRowAsync();
            await import.WriteAsync(streamId, NpgsqlDbType.Uuid);
            await import.WriteAsync(batch.Seq, NpgsqlDbType.Integer);
            await import.WriteAsync((short)i, NpgsqlDbType.Smallint);
            await import.WriteAsync(entry.Time, NpgsqlDbType.Real);
            await import.WriteAsync(receivedAt.AddMilliseconds(-entry.AgeMs), NpgsqlDbType.TimestampTz);
            await import.WriteAsync(entry.Name, NpgsqlDbType.Text);
            await import.WriteAsync(entry.Fields, NpgsqlDbType.Jsonb);
        }

        await import.CompleteAsync();
    }

    private static async Task UpsertTotals(
        NpgsqlConnection connection,
        MatchMetricStream stream,
        ParsedMatchBatch batch,
        Dictionary<int, MatchMetricSlot> added)
    {
        var totals = MatchMetricTotals.Of(batch.Chunks, slot => SlotOf(stream, added, slot).SeriesId);

        if (totals.Count == 0)
            return;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
                               INSERT INTO {MatchMetricsSchema.Totals} AS total (stream_id, series_id, sum, count, max)
                               SELECT @id, series, sum, count, max FROM unnest(@series, @sums, @counts, @maxes) AS t(series, sum, count, max)
                               ON CONFLICT (stream_id, series_id) DO UPDATE SET
                                   sum = total.sum + EXCLUDED.sum,
                                   count = total.count + EXCLUDED.count,
                                   max = GREATEST(total.max, EXCLUDED.max)
                               """;
        command.Parameters.AddWithValue("id", stream.Id);
        command.Parameters.AddWithValue("series", totals.Select(total => total.SeriesId).ToArray());
        command.Parameters.AddWithValue("sums", totals.Select(total => total.Sum).ToArray());
        command.Parameters.AddWithValue("counts", totals.Select(total => total.Count).ToArray());
        command.Parameters.AddWithValue("maxes", totals.Select(total => total.Max).ToArray());
        await command.ExecuteNonQueryAsync();
    }

    private static MatchMetricSlot SlotOf(MatchMetricStream stream, Dictionary<int, MatchMetricSlot> added, int slot)
    {
        return stream.Slots.TryGetValue(slot, out var known) ? known : added[slot];
    }
}
