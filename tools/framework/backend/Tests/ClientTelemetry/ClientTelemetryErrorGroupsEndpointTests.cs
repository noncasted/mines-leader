using System.Text.Json;
using FluentAssertions;
using Game.ClientTelemetry;
using Infrastructure.State;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The collection of the cluster is shared with the other tests: a test looks only at its own groups, by a word of its messages.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryErrorGroupsEndpointTests : ClientTelemetryEndpointTest
{
    public ClientTelemetryErrorGroupsEndpointTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task Errors_GoByFrequencyAndFreshness_WithTheSessions()
    {
        var host = await StartHost();
        var word = Word();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await Close(first, Error($"{word}Older", "Game.Foo:Older () (at Assets/Foo.cs:1)", 5), Error($"{word}Often", "Game.Foo:Often ()", 2));

        Cluster.Clock.Advance(TimeSpan.FromSeconds(10));

        await Close(second, Error($"{word}Newer", "Game.Foo:Newer ()", 5), Error($"{word}Often", "Game.Foo:Often ()", 7));

        var answer = await Get(host, "/errors?limit=5000");
        var groups = Own(answer, word);

        groups.Select(group => group.GetProperty("message").GetString()).Should().Equal(
            $"{word}Often: boom <n>",
            $"{word}Newer: boom <n>",
            $"{word}Older: boom <n>");

        var often = groups[0];
        often.GetProperty("count").GetInt64().Should().Be(9);
        often.GetProperty("sessions").GetInt32().Should().Be(2);
        often.GetProperty("level").GetString().Should().Be("exception");
        often.GetProperty("fingerprint").GetString().Should().HaveLength(16);
        often.GetProperty("sessionIds").EnumerateArray().Select(id => id.GetGuid()).Should().Equal(second, first);

        answer.GetProperty("totalGroups").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        answer.GetProperty("countedSessions").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ALiveSession_IsNotInTheGroups_UntilItsClose()
    {
        var host = await StartHost();
        var word = Word();
        var session = Guid.NewGuid();
        var withoutDigest = (await Get(host, "/errors")).GetProperty("sessionsWithoutDigest").GetInt32();

        await Store(Request(session, 1, errors: [Error($"{word}Live", "", 4)]));

        var live = await Get(host, "/errors?limit=5000");
        Own(live, word).Should().BeEmpty();
        live.GetProperty("sessionsWithoutDigest").GetInt32().Should().Be(withoutDigest + 1);

        await Store(Request(session, 2, ended: true));

        var closed = await Get(host, "/errors?limit=5000");
        Own(closed, word).Single().GetProperty("count").GetInt64().Should().Be(4);
        closed.GetProperty("sessionsWithoutDigest").GetInt32().Should().Be(withoutDigest);
    }

    // The way of another host: it knows a closed session only by reading its row.
    [Fact]
    public async Task TheGroupsOfAClosedSession_SurviveTheReadingFromTheState()
    {
        var word = Word();
        var session = Guid.NewGuid();

        await Close(session, Error($"{word}Stored", "Game.Foo:Stored () (at Assets/Foo.cs:1)", 3));

        var info = Cluster.Storage.Registry.Get<TelemetrySessionState>();
        var identity = new StateIdentity { Key = session, Type = info.Name, TableName = info.TableName, Extension = null };
        var state = await Cluster.Storage.Read<TelemetrySessionState>(identity);

        var group = state.Digest!.ErrorGroups.Groups.Single();
        group.Count.Should().Be(3);
        group.Rows.Should().Be(1);
        group.Message.Should().Be($"{word}Stored: boom <n>");
        group.Level.Should().Be(TelemetryErrorLevel.Exception);

        await Cluster.Collection.OnUpdated(session, state);

        var fromCollection = await Reader.GetErrorGroups(5000);
        fromCollection.Groups.Single(item => item.Message.StartsWith(word)).SessionIds.Should().Equal(session);
    }

    [Fact]
    public async Task TheRows_OfALiveAndOfAClosedSession_HaveTheFingerprintOfTheGroup()
    {
        var word = Word();
        var session = Guid.NewGuid();

        await Store(Request(session, 1, errors: [Error($"{word}Row", "Game.Foo:Row () (at Assets/Foo.cs:7)", 1)]));

        var live = (await Reader.GetErrors(session, 0, 10)).Single().Fingerprint;
        live.Should().NotBeNullOrEmpty();

        await Store(Request(session, 2, ended: true));

        (await Reader.GetErrors(session, 0, 10)).Single().Fingerprint.Should().Be(live);
        (await Session(session)).Digest!.ErrorGroups.Groups.Single().Fingerprint.Should().Be(live);
    }

    // The same template in two categories: two groups, each with its category; an error of the log of Unity has none.
    [Fact]
    public async Task TheCategory_IsInTheRowsAndTheGroups()
    {
        var host = await StartHost();
        var word = Word();
        var session = Guid.NewGuid();

        await Close(session,
            TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, $"{word} join failed", "", 2, "Global.Lobbies.LobbyJoin"),
            TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, $"{word} join failed", "", 3, "Global.Network.PeerSocket"),
            TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, $"{word} join failed", "", 1));

        var groups = Own(await Get(host, "/errors?limit=5000"), word);

        groups.Select(group => group.GetProperty("category").GetString()).Should().BeEquivalentTo(
            new[] { "Global.Network.PeerSocket", "Global.Lobbies.LobbyJoin", null });
        groups.Select(group => group.GetProperty("fingerprint").GetString()).Distinct().Should().HaveCount(3);

        var rows = (await Get(host, $"/sessions/{session}/errors")).GetProperty("items").EnumerateArray().ToList();
        rows.Select(row => row.GetProperty("category").GetString()).Should().Equal("Global.Lobbies.LobbyJoin", "Global.Network.PeerSocket", null);
        (await Session(session)).Digest!.ErrorGroups.Groups.Select(group => group.Category).Should().BeEquivalentTo(
            new[] { "Global.Network.PeerSocket", "Global.Lobbies.LobbyJoin", null });
    }

    private Task Close(Guid session, params SharedClientTelemetry.Error[] errors)
    {
        return Store(Request(session, 1, ended: true, errors: errors));
    }

    private static SharedClientTelemetry.Error Error(string type, string stack, int repeat)
    {
        return TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Exception, $"{type}: boom 42", stack, repeat);
    }

    private static List<JsonElement> Own(JsonElement answer, string word)
    {
        return answer.GetProperty("groups").EnumerateArray()
            .Where(group => group.GetProperty("message").GetString()!.StartsWith(word))
            .ToList();
    }

    // Letters out of hex: the scrubber keeps it as a name of code.
    private static string Word()
    {
        return "T" + new string(Guid.NewGuid().ToString("N")[..12].Select(symbol => (char)('g' + Convert.ToInt32(symbol.ToString(), 16))).ToArray());
    }
}
