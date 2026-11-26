using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.MatchMetrics;
using Internal;
using Shared;
using Xunit;

namespace Framework.Tests.MatchMetrics;

// The stream of the server: no admission, user Guid.Empty, its values written by the flush timer and at its end.
public class MatchMetricsServerStreamTests : LifetimeTest, IClassFixture<MatchMetricsDatabase>
{
    public MatchMetricsServerStreamTests(MatchMetricsDatabase database)
    {
        _database = database;
    }

    private readonly MatchMetricsDatabase _database;
    private readonly Guid _match = Guid.NewGuid();

    private Task<long> Count(string table)
    {
        return _database.Scalar<long>(
            $"SELECT count(*) FROM {table} t JOIN match_metric_streams s ON s.id = t.stream_id WHERE s.match_id = @m AND s.kind = 1 AND s.user_id = @u",
            ("m", _match), ("u", Guid.Empty));
    }

    [Fact]
    public async Task The_end_of_the_lifetime_writes_events_and_gauges()
    {
        var lifetime = new Lifetime();
        var stream = _database.ServerWriter(new MatchMetricsOptions { ServerFlush = TimeSpan.FromHours(1) }).Open(lifetime, _match);

        stream.Event("session.host", new SharedMatchMetrics.MatchEventField { Key = "index", Number = 1 });
        stream.Gauge("session.users", 2);
        stream.Gauge("session.users", 3);
        stream.Event("session.away", new SharedMatchMetrics.MatchEventField { Key = "seat", Number = 0 });

        lifetime.Terminate();

        await TestWait.Until(async () => await Count("match_metric_events") == 2);
        (await Count("match_metric_chunks")).Should().Be(1);
        (await _database.Scalar<float>(
                "SELECT t.max FROM match_metric_totals t JOIN match_metric_streams s ON s.id = t.stream_id WHERE s.match_id = @m",
                ("m", _match)))
            .Should().Be(3);
    }

    [Fact]
    public async Task The_timer_writes_the_seconds_over_and_the_end_the_rest()
    {
        var lifetime = new Lifetime();
        var stream = _database.ServerWriter(new MatchMetricsOptions { ServerFlush = TimeSpan.FromMilliseconds(200) }).Open(lifetime, _match);

        stream.Event("session.host", new SharedMatchMetrics.MatchEventField { Key = "index", Number = 0 });
        await TestWait.Until(async () => await Count("match_metric_events") == 1);

        stream.Event("session.return", new SharedMatchMetrics.MatchEventField { Key = "seat", Number = 0 });
        lifetime.Terminate();

        await TestWait.Until(async () => await Count("match_metric_events") == 2);
        (await _database.Scalar<int>("SELECT last_seq FROM match_metric_streams WHERE match_id = @m", ("m", _match))).Should().Be(2);
    }

    [Fact]
    public void A_bad_name_throws()
    {
        var stream = _database.ServerWriter(new MatchMetricsOptions()).Open(TestLifetime, _match);

        var write = () => stream.Event("Session Host");
        write.Should().Throw<ArgumentException>();
    }
}
