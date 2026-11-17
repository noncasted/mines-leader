using System.Net;
using System.Text.Json;
using FluentAssertions;
using Meta.Matches;
using Meta.Users;
using Xunit;

namespace Tests.Meta.Matches;

// The players of a match in the read API: the totals and events of each participant from the collector of the game
// metrics, the series of one of them and its csv, 404 for an unknown match or a stranger.
public class MatchHistoryPlayersTests
{
    private static readonly DateTime Start = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly MatchSummaryCollectionFake _summaries = new();
    private readonly UserCollectionFake _users = new();
    private readonly MatchMetricsReaderFake _reader = new();

    private readonly Guid _matchId = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bot = Guid.NewGuid();

    public MatchHistoryPlayersTests()
    {
        _users.Add(_alice, new UserState { UserId = _alice, Name = "Alice" });
        _summaries.Add(_matchId, TestMatches.Create(Start, [(_alice, "Alice"), (_bot, "Bot")], _alice, TimeSpan.FromMinutes(3)));

        _reader.AddStream(_matchId, _alice);
        _reader.AddCounter(_alice, MatchGameMetrics.CellsOpened, Tag("source", "click"), (1, 1), (12, 1));
        _reader.AddCounter(_alice, MatchGameMetrics.CellsOpened, Tag("source", "cascade"), (12, 9));
        _reader.AddCounter(_alice, MatchGameMetrics.Cascades, Tag(), (12, 1));
        _reader.AddCounter(_alice, MatchGameMetrics.FlagsSet, Tag(), (20, 2));
        _reader.AddCounter(_alice, MatchGameMetrics.CardsPlayed, Tag("card", "trebuchet"), (25, 1));
        _reader.AddEvent(_alice, MatchGameMetrics.CardPlay, 25, new { card = "trebuchet", board = "enemy", cells = 4 });
        _reader.AddEvent(_alice, MatchGameMetrics.TurnEnd, 30, new { number = 1, seconds = 30, cells = 11, cards = 1 });
        _reader.AddEvent(_alice, MatchGameMetrics.MatchEnd, 180, new { result = "win", seconds = 180 });
    }

    [Fact]
    public async Task Players_TotalsAndEvents_BotWithoutMetrics()
    {
        await using var host = await StartHost();

        var players = await host.GetJson($"/api/match-history/matches/{_matchId}/players");

        players.GetArrayLength().Should().Be(2);

        var alice = players[0];
        alice.GetProperty("userId").GetGuid().Should().Be(_alice);
        alice.GetProperty("nickname").GetString().Should().Be("Alice");
        alice.GetProperty("isWinner").GetBoolean().Should().BeTrue();
        alice.GetProperty("streams").GetInt32().Should().Be(1);
        alice.GetProperty("cellsOpened").GetDouble().Should().Be(11);
        alice.GetProperty("cellsBySource").GetProperty("click").GetDouble().Should().Be(2);
        alice.GetProperty("cellsBySource").GetProperty("cascade").GetDouble().Should().Be(9);
        alice.GetProperty("counters").GetProperty("cascades").GetDouble().Should().Be(1);
        alice.GetProperty("counters").GetProperty("flags.set").GetDouble().Should().Be(2);
        alice.GetProperty("counters").GetProperty("mines.hit").GetDouble().Should().Be(0);
        alice.GetProperty("cardsByType").GetProperty("trebuchet").GetDouble().Should().Be(1);
        alice.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("name").GetString())
             .Should().Equal("card.play", "turn.end", "match.end");
        alice.GetProperty("events")[0].GetProperty("fields").GetProperty("board").GetString().Should().Be("enemy");

        var bot = players[1];
        bot.GetProperty("streams").GetInt32().Should().Be(0);
        bot.GetProperty("cellsOpened").GetDouble().Should().Be(0);
        bot.GetProperty("events").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Series_LinesBySteps()
    {
        await using var host = await StartHost();

        var series = await host.GetJson($"/api/match-history/matches/{_matchId}/players/{_alice}/series?step=10");

        series.GetProperty("step").GetInt32().Should().Be(10);
        series.GetProperty("axis").GetString().Should().Be("Run");

        var lines = series.GetProperty("lines").EnumerateArray()
                          .ToDictionary(line => line.GetProperty("key").GetString()!, line => Points(line.GetProperty("points")));

        lines.Keys.Should().Equal("cells.opened:cascade", "cells.opened:click", "cascades", "flags.set", "cards.played:trebuchet");
        lines["cells.opened:click"].Should().Equal((0L, 1d), (10L, 1d));
        lines["flags.set"].Should().Equal((20L, 2d));
    }

    [Fact]
    public async Task SeriesCsv_RowPerPoint()
    {
        await using var host = await StartHost();

        var response = await host.Get($"/api/match-history/matches/{_matchId}/players/{_alice}/series.csv?step=60");
        var text = await response.Content.ReadAsStringAsync();

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(
            "time,series,value",
            "0,cells.opened:cascade,9",
            "0,cells.opened:click,2",
            "0,cascades,1",
            "0,flags.set,2",
            "0,cards.played:trebuchet,1");
    }

    [Theory]
    [InlineData("step=5")]
    [InlineData("axis=wall")]
    public async Task Series_BadParameter_BadRequest(string query)
    {
        await using var host = await StartHost();

        var response = await host.Get($"/api/match-history/matches/{_matchId}/players/{_alice}/series?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownMatchOrStranger_NotFound()
    {
        await using var host = await StartHost();

        (await host.Get($"/api/match-history/matches/{Guid.NewGuid()}/players")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.Get($"/api/match-history/matches/{_matchId}/players/{Guid.NewGuid()}/series")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    private Task<MatchHistoryHost> StartHost()
    {
        return MatchHistoryHost.Start(_summaries, _users, _reader);
    }

    private static IReadOnlyDictionary<string, string> Tag(params string[] pair)
    {
        return pair.Length == 0 ? new Dictionary<string, string>() : new Dictionary<string, string> { [pair[0]] = pair[1] };
    }

    private static IReadOnlyList<(long, double)> Points(JsonElement points)
    {
        return points.EnumerateArray().Select(point => (point.GetProperty("time").GetInt64(), point.GetProperty("value").GetDouble())).ToArray();
    }
}
