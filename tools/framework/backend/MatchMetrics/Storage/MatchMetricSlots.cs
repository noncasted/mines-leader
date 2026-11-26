using Npgsql;
using Shared;

namespace Game.MatchMetrics;

// The slot map of a stream as stored: read by an Open and by a Duplicate, whose first copy may have declared slots the
// stream in memory has not seen.
internal static class MatchMetricSlots
{
    public static async Task<Dictionary<int, MatchMetricSlot>> Read(NpgsqlConnection connection, Guid streamId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
                               SELECT slot.slot, slot.series_id, series.kind
                               FROM {MatchMetricsSchema.Slots} slot JOIN {MatchMetricsSchema.Series} series ON series.id = slot.series_id
                               WHERE slot.stream_id = @id
                               """;
        command.Parameters.AddWithValue("id", streamId);

        var slots = new Dictionary<int, MatchMetricSlot>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            slots.Add(reader.GetInt16(0), new MatchMetricSlot(reader.GetInt32(1), (SharedMatchMetrics.MatchSeriesKind)reader.GetInt16(2)));

        return slots;
    }
}
