using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shared;

namespace DeploySetup;

// Deletes the records of matches (IMatch, event state MatchState) of the old format that never close: set up without
// the names of the participants, with no MatchCompleted and no MatchClosed. The old MatchFactory created such records
// (a new Guid per match, type Single, Setup only); after the close of the matches was added they stay open forever and
// the history lists show them as running. A record of the new format (names at the setup) is never deleted.
// A record is removed fully: its events, its stream and its inline snapshot (the document IMatchSummaryCollection and
// the history pages read). Idempotent: a second run finds nothing.
public static class DeadMatchRecordsCleanup
{
    // Marten names of the event store and of the inline snapshot of MatchState (MartenSetupExtensions).
    private const string StreamsTable = "mt_streams";
    private const string EventsTable = "mt_events";
    private const string SnapshotsTable = "mt_doc_matchstate";

    private const string SetupEvent = "match_setup";
    private const string CompletedEvent = "match_completed";
    private const string ClosedEvent = "match_closed";

    // The number of deleted records.
    public static async Task<int> Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        // A database the hosts have not started on yet has no event store: nothing to clean.
        if (await connection.IsTableExists(StreamsTable) == false)
        {
            System.Console.WriteLine("[DeploySetup] Dead match records: no event store yet, nothing to delete");
            return 0;
        }

        var deleted = await Delete(connection, StatesLookup.Match.StateName + ":");

        System.Console.WriteLine($"[DeploySetup] Dead match records deleted: {deleted}");
        return deleted;
    }

    private static async Task<int> Delete(NpgsqlConnection connection, string streamPrefix)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        var select = $"""
                      CREATE TEMP TABLE dead_match_streams ON COMMIT DROP AS
                      SELECT s.id FROM {StreamsTable} s
                      WHERE starts_with(s.id, @prefix)
                        AND EXISTS (
                            SELECT 1 FROM {EventsTable} e
                            WHERE e.stream_id = s.id AND e.type = '{SetupEvent}')
                        AND NOT EXISTS (
                            SELECT 1 FROM {EventsTable} e
                            WHERE e.stream_id = s.id
                              AND (e.type IN ('{CompletedEvent}', '{ClosedEvent}')
                                   OR (e.type = '{SetupEvent}'
                                       AND jsonb_typeof(e.data -> 'Nicknames') = 'object'
                                       AND EXISTS (
                                           SELECT 1 FROM jsonb_object_keys(e.data -> 'Nicknames') AS k
                                           WHERE k <> '$type'))));
                      """;

        await using (var command = new NpgsqlCommand(select, connection, transaction))
        {
            command.Parameters.AddWithValue("prefix", streamPrefix);
            await command.ExecuteNonQueryAsync();
        }

        int count;

        await using (var command = new NpgsqlCommand("SELECT count(*) FROM dead_match_streams", connection, transaction))
            count = (int)(long)(await command.ExecuteScalarAsync()).ThrowIfNull();

        if (count == 0)
        {
            await transaction.CommitAsync();
            return 0;
        }

        // The snapshot table exists whenever a match stream does: a missing one throws.
        var delete = $"""
                      DELETE FROM {SnapshotsTable} WHERE id IN (SELECT id FROM dead_match_streams);
                      DELETE FROM {EventsTable} WHERE stream_id IN (SELECT id FROM dead_match_streams);
                      DELETE FROM {StreamsTable} WHERE id IN (SELECT id FROM dead_match_streams);
                      """;

        await using (var command = new NpgsqlCommand(delete, connection, transaction))
            await command.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
        return count;
    }
}
