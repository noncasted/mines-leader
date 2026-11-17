using FluentAssertions;
using Game.MatchMetrics;
using Shared;
using Xunit;

namespace Framework.Tests.MatchMetrics;

// The queries of the reader over a stored match of two players (MatchMetricsReadSeed).
public class MatchMetricsReaderTests : IClassFixture<MatchMetricsDatabase>, IAsyncLifetime
{
    public MatchMetricsReaderTests(MatchMetricsDatabase database)
    {
        _database = database;
        _reader = new MatchMetricsReader(database);
    }

    private readonly MatchMetricsDatabase _database;
    private readonly MatchMetricsReader _reader;
    private readonly MatchMetricsReadSeed _seed = new();

    public async ValueTask InitializeAsync()
    {
        await _seed.Write(_database);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private Task<IReadOnlyList<MatchMetricGroupPoints>> Points(MatchMetricFilter filter, MatchMetricStep step = MatchMetricStep.Second, long? from = null)
    {
        return _reader.GetPoints(_seed.Match, new MatchMetricPointsQuery { Filter = filter, Step = step, From = from });
    }

    private static (long, double)[] Values(MatchMetricGroupPoints group)
    {
        return group.Points.Select(point => (point.Time, point.Value)).ToArray();
    }

    [Fact]
    public async Task Streams_are_the_writers_of_the_match()
    {
        var streams = await _reader.GetStreams(_seed.Match);

        streams.Select(stream => stream.UserId).Should().BeEquivalentTo([_seed.A, _seed.B]);
        streams.Single(stream => stream.UserId == _seed.A).LastSeq.Should().Be(2);
    }

    [Fact]
    public async Task Points_by_source_sum_the_players_per_second()
    {
        var groups = await Points(new MatchMetricFilter { Name = "damage.dealt", GroupBy = ["source"] });

        groups.Select(group => group.Group["source"]).Should().Equal("bolt", "shot");
        Values(groups[0]).Should().Equal((0, 5), (1, 5));
        Values(groups[1]).Should().Equal((0, 11), (1, 21), (2, 30), (12, 7));
    }

    [Fact]
    public async Task A_step_of_ten_seconds_sums_its_buckets_and_a_window_cuts_the_steps()
    {
        var groups = await Points(new MatchMetricFilter { Name = "damage.dealt" }, MatchMetricStep.TenSeconds);
        Values(groups.Single()).Should().Equal((0, 72), (10, 7));

        var late = await Points(new MatchMetricFilter { Name = "damage.dealt" }, MatchMetricStep.TenSeconds, from: 10);
        Values(late.Single()).Should().Equal((10, 7));
    }

    [Fact]
    public async Task A_gauge_is_the_last_value_of_a_step()
    {
        var seconds = await Points(new MatchMetricFilter { Name = "hearts" });
        Values(seconds.Single()).Should().Equal((0, 3), (2, 2));
        seconds.Single().Kind.Should().Be(SharedMatchMetrics.MatchSeriesKind.Gauge);

        var steps = await Points(new MatchMetricFilter { Name = "hearts" }, MatchMetricStep.TenSeconds);
        Values(steps.Single()).Should().Equal((0, 2));
    }

    [Fact]
    public async Task A_tag_and_a_user_filter_and_the_user_groups()
    {
        var groups = await Points(new MatchMetricFilter
        {
            Name = "damage.dealt",
            Tags = new Dictionary<string, string> { ["source"] = "shot" },
            GroupBy = [MatchMetricFilter.UserKey]
        });

        groups.Should().HaveCount(2);
        Values(groups.Single(group => group.Group["user"] == _seed.B.ToString())).Should().Equal((0, 1), (1, 1));

        var mine = await Points(new MatchMetricFilter { Name = "damage.dealt", UserId = _seed.B });
        Values(mine.Single()).Should().Equal((0, 1), (1, 1));
    }

    [Fact]
    public async Task The_server_axis_keeps_the_values_on_unix_seconds()
    {
        var groups = await _reader.GetPoints(_seed.Match, new MatchMetricPointsQuery
        {
            Filter = new MatchMetricFilter { Name = "damage.dealt" },
            Axis = MatchMetricAxis.Server
        });

        groups.Single().Points.Sum(point => point.Value).Should().Be(79);
        groups.Single().Points.Should().OnlyContain(point => point.Time > 1_700_000_000);
    }

    [Fact]
    public async Task Totals_of_the_match_group_by_card()
    {
        var totals = await _reader.GetTotals(new MatchMetricTotalsQuery
        {
            MatchId = _seed.Match,
            Filter = new MatchMetricFilter { Name = "damage.dealt", GroupBy = ["card"] }
        });

        totals.Select(total => (total.Group["card"], total.Sum, total.Streams)).Should().Equal(("", 69d, 2), ("lightning", 10d, 1));
    }

    [Fact]
    public async Task Totals_across_matches_take_the_streams_opened_in_the_window()
    {
        var filter = new MatchMetricFilter { Name = "damage.dealt", UserId = _seed.A };

        var all = await _reader.GetTotals(new MatchMetricTotalsQuery { Filter = filter, Since = _database.Time.GetUtcNow().UtcDateTime.AddHours(-1) });
        all.Single().Sum.Should().Be(77);

        var later = await _reader.GetTotals(new MatchMetricTotalsQuery { Filter = filter, Since = _database.Time.GetUtcNow().UtcDateTime.AddHours(1) });
        later.Should().BeEmpty();
    }

    [Fact]
    public async Task Events_filter_by_name_user_and_time()
    {
        var all = await _reader.GetEvents(_seed.Match, new MatchMetricEventsQuery());
        all.Select(entry => entry.Name).Should().Equal("upgrade.pick", "player.death");
        all[0].Fields.GetProperty("card").GetDouble().Should().Be(12);

        (await _reader.GetEvents(_seed.Match, new MatchMetricEventsQuery { UserId = _seed.B })).Single().Name.Should().Be("player.death");
        (await _reader.GetEvents(_seed.Match, new MatchMetricEventsQuery { Name = "upgrade.pick" })).Should().ContainSingle();
        (await _reader.GetEvents(_seed.Match, new MatchMetricEventsQuery { From = 3, To = 10 })).Single().Time.Should().Be(8);
    }

    [Fact]
    public async Task The_dictionary_lists_the_series_with_their_tags()
    {
        var series = await _reader.GetSeries("damage.dealt");

        series.Should().Contain(one => one.Tags.Count == 2 && one.Tags["card"] == "lightning");
        series.Should().OnlyContain(one => one.Name == "damage.dealt");
    }

    [Fact]
    public async Task A_query_out_of_the_rules_throws()
    {
        var name = () => Points(new MatchMetricFilter { Name = "Damage; DROP" });
        await name.Should().ThrowAsync<MatchMetricsQueryException>();

        var key = () => Points(new MatchMetricFilter { GroupBy = ["source", "source"] });
        await key.Should().ThrowAsync<MatchMetricsQueryException>();

        var mixed = () => Points(new MatchMetricFilter());
        await mixed.Should().NotThrowAsync();
    }
}
