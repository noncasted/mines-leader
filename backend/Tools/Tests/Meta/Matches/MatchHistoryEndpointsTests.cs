using System.Net;
using System.Text.Json;
using FluentAssertions;
using Meta.Users;
using Xunit;

namespace Tests.Meta.Matches;

// The read API of the match records (MetaGateway): the token, the filters of the list, a match, the matches of a player.
public class MatchHistoryEndpointsTests
{
    private static readonly DateTime Start = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly MatchSummaryCollectionFake _summaries = new();
    private readonly UserCollectionFake _users = new();

    [Theory]
    [InlineData("/api/match-history/matches")]
    [InlineData("/api/match-history/players/Somebody")]
    public async Task WithoutToken_Unauthorized(string path)
    {
        await using var host = await StartHost();

        var response = await host.Get(path, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_NewestFirst_WithPlayersAndClose()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        var older = AddMatch(Start, alice, bob, winner: alice, closedAfter: TimeSpan.FromMinutes(5));
        var newer = AddMatch(Start.AddHours(1), alice, bob);
        await using var host = await StartHost();

        var rows = await host.GetJson("/api/match-history/matches");

        Ids(rows).Should().Equal(newer, older);

        var closed = rows[1];
        closed.GetProperty("type").GetString().Should().Be("LastManStandingTurnBased");
        closed.GetProperty("winner").GetGuid().Should().Be(alice);
        closed.GetProperty("durationSeconds").GetDouble().Should().Be(300);
        closed.GetProperty("players")[0].GetProperty("nickname").GetString().Should().Be("Alice");
        closed.GetProperty("players")[0].GetProperty("isWinner").GetBoolean().Should().BeTrue();
        closed.GetProperty("players")[1].GetProperty("ratingChange").GetInt32().Should().Be(-20);
        closed.GetProperty("players")[0].GetProperty("deck")[0].GetString().Should().Be("Trebuchet");

        rows[0].GetProperty("closedAt").ValueKind.Should().Be(JsonValueKind.Null);
        rows[0].GetProperty("winner").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_Filters()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        var carl = AddUser("Carl");
        var won = AddMatch(Start, alice, bob, winner: alice, closedAfter: TimeSpan.FromMinutes(5));
        var noWinner = AddMatch(Start.AddMinutes(10), alice, carl, closedAfter: TimeSpan.FromMinutes(1));
        var running = AddMatch(Start.AddMinutes(20), bob, carl);
        await using var host = await StartHost();

        Ids(await host.GetJson($"/api/match-history/matches?player={alice}")).Should().Equal(noWinner, won);
        Ids(await host.GetJson("/api/match-history/matches?player=carl")).Should().Equal(running, noWinner);
        Ids(await host.GetJson("/api/match-history/matches?closed=false")).Should().Equal(running);
        Ids(await host.GetJson("/api/match-history/matches?closed=true&winner=false")).Should().Equal(noWinner);
        Ids(await host.GetJson("/api/match-history/matches?winner=true")).Should().Equal(won);
        Ids(await host.GetJson("/api/match-history/matches?from=2026-11-17T12:05:00Z&to=2026-11-17T12:20:00Z")).Should().Equal(noWinner);
        Ids(await host.GetJson("/api/match-history/matches?type=Single")).Should().BeEmpty();
        Ids(await host.GetJson("/api/match-history/matches?offset=1&limit=1")).Should().Equal(noWinner);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=1001")]
    [InlineData("offset=-1")]
    [InlineData("closed=maybe")]
    public async Task List_BadParameter_BadRequest(string query)
    {
        await using var host = await StartHost();

        var response = await host.Get($"/api/match-history/matches?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Match_KnownAndUnknown()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        var id = AddMatch(Start, alice, bob, closedAfter: TimeSpan.FromMinutes(2));
        await using var host = await StartHost();

        var row = await host.GetJson($"/api/match-history/matches/{id}");
        row.GetProperty("id").GetGuid().Should().Be(id);
        row.GetProperty("closedAt").GetDateTime().Should().Be(Start.AddMinutes(2));

        var missing = await host.Get($"/api/match-history/matches/{Guid.NewGuid()}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Player_ByIdAndName_NotFound_Conflict()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        AddUser("Twin");
        AddUser("twin");
        var first = AddMatch(Start, alice, bob);
        var second = AddMatch(Start.AddHours(1), alice, bob);
        await using var host = await StartHost();

        var byName = await host.GetJson("/api/match-history/players/alice?limit=1");
        byName.GetProperty("userId").GetGuid().Should().Be(alice);
        byName.GetProperty("total").GetInt32().Should().Be(2);
        Ids(byName.GetProperty("matches")).Should().Equal(second);

        var byId = await host.GetJson($"/api/match-history/players/{bob}");
        Ids(byId.GetProperty("matches")).Should().Equal(second, first);

        (await host.Get("/api/match-history/players/Nobody")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.Get("/api/match-history/players/twin")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private Guid AddUser(string name)
    {
        var id = Guid.NewGuid();
        _users.Add(id, new UserState { UserId = id, Name = name });
        return id;
    }

    private Guid AddMatch(DateTime start, Guid first, Guid second, Guid? winner = null, TimeSpan? closedAfter = null)
    {
        var id = Guid.NewGuid();
        _summaries.Add(id, TestMatches.Create(start, [(first, _users[first].Name), (second, _users[second].Name)], winner, closedAfter));
        return id;
    }

    private Task<MatchHistoryHost> StartHost()
    {
        return MatchHistoryHost.Start(_summaries, _users, new MatchMetricsReaderFake());
    }

    private static IReadOnlyList<Guid> Ids(JsonElement rows)
    {
        return rows.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToArray();
    }
}
