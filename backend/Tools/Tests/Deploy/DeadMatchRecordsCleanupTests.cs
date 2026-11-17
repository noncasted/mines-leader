using DeploySetup;
using FluentAssertions;
using Infrastructure.State;
using JasperFx;
using Marten;
using Meta.Matches;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Npgsql;
using Orchestration;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Deploy;

/// <summary>
/// The migrator deletes the records of matches of the old format that never close (no names, no MatchCompleted,
/// no MatchClosed): their events, streams and snapshots. A completed old record and every record of the new format stay.
/// Every test runs against its own database with the Marten settings of the hosts.
/// </summary>
public class DeadMatchRecordsCleanupTests : IAsyncLifetime
{
    private readonly DatabaseFixture _database = new();
    private DocumentStore _store = null!;
    private string _streamPrefix = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();

        var states = new List<GrainStateInfo>();
        GeneratedStatesRegistration.AddAllStates(states);
        var registry = new GrainStatesRegistry(states);

        _streamPrefix = registry.Get<MatchState>().Name + ":";

        _store = DocumentStore.For(options => {
            options.Connection(_database.DataSource);
            options.AutoCreateSchemaObjects = AutoCreate.All;
            MartenSetupExtensions.Configure(options, registry);
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Run_DeletesOnlyDeadOldRecords_SecondRunDeletesNothing()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var oldDead = await Append(OldSetup(first, second));
        var oldDeadWithoutKey = await Append(OldSetup(first, second));
        await StripNicknames(oldDeadWithoutKey);
        var oldCompleted = await Append(OldSetup(first, second), new MatchCompleted(first, TimeSpan.FromMinutes(3)));
        var newOpen = await Append(NewSetup(first, second));
        var newClosed = await Append(NewSetup(first, second), new MatchClosed(DateTime.UtcNow));

        (await ReadRecords()).Keys.Should().BeEquivalentTo([oldDead, oldDeadWithoutKey, oldCompleted, newOpen, newClosed]);

        (await DeadMatchRecordsCleanup.Run(CreateConfiguration())).Should().Be(2);

        var records = await ReadRecords();
        records.Keys.Should().BeEquivalentTo([oldCompleted, newOpen, newClosed]);
        records[oldCompleted].Winner.Should().Be(first);
        records[newOpen].NicknameOf(first).Should().Be("Alice");

        foreach (var dead in new[] { oldDead, oldDeadWithoutKey })
        {
            var streamId = _streamPrefix + dead.ToString("D");
            (await Count("mt_events", "stream_id", streamId)).Should().Be(0);
            (await Count("mt_streams", "id", streamId)).Should().Be(0);
            (await Count("mt_doc_matchstate", "id", streamId)).Should().Be(0);
        }

        (await DeadMatchRecordsCleanup.Run(CreateConfiguration())).Should().Be(0);
        (await ReadRecords()).Keys.Should().BeEquivalentTo([oldCompleted, newOpen, newClosed]);
    }

    [Fact]
    public async Task Run_NoEventStore_DeletesNothing()
    {
        (await DeadMatchRecordsCleanup.Run(CreateConfiguration())).Should().Be(0);
    }

    // The old setup had no names: an empty dictionary here, a missing key after StripNicknames.
    private static MatchSetup OldSetup(Guid first, Guid second)
    {
        return new MatchSetup(GameMatchType.Single, DateTime.UtcNow, [first, second], Decks(first, second), new());
    }

    private static MatchSetup NewSetup(Guid first, Guid second)
    {
        return new MatchSetup(GameMatchType.LastManStandingTurnBased, DateTime.UtcNow, [first, second],
            Decks(first, second), new Dictionary<Guid, string> { [first] = "Alice", [second] = "Bob" });
    }

    private static Dictionary<Guid, IReadOnlyList<CardType>> Decks(Guid first, Guid second)
    {
        return new Dictionary<Guid, IReadOnlyList<CardType>>
        {
            [first] = new List<CardType> { CardType.Trebuchet },
            [second] = new List<CardType> { CardType.Trebuchet }
        };
    }

    // As EventState writes a grain: the events of the stream "<state name>:<grain key>", the snapshot inline.
    private async Task<Guid> Append(params object[] events)
    {
        var id = Guid.NewGuid();

        await using var session = _store.LightweightSession();
        session.Events.Append(_streamPrefix + id.ToString("D"), events);
        await session.SaveChangesAsync();

        return id;
    }

    private Task StripNicknames(Guid id)
    {
        return Execute("UPDATE mt_events SET data = data - 'Nicknames' WHERE stream_id = @id",
            _streamPrefix + id.ToString("D"));
    }

    // The read of IMatchSummaryCollection on load (StateCollection over the event storage).
    private async Task<IReadOnlyDictionary<Guid, MatchState>> ReadRecords()
    {
        var storage = new EventStorage(_store, Substitute.For<IStateSerializer>());
        var result = new Dictionary<Guid, MatchState>();

        await foreach (var (key, value) in storage.ReadAll<Guid, MatchState>(_streamPrefix, GrainKeyType.Guid))
            result.Add(key, value);

        return result;
    }

    private IConfigurationManager CreateConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration["postgres"] = _database.ConnectionString;
        return configuration;
    }

    private async Task<long> Count(string table, string column, string id)
    {
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE {column} = @id", connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task Execute(string sql, string id)
    {
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }
}
