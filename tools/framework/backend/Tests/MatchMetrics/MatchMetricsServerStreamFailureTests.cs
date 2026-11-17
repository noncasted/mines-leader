using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.MatchMetrics;
using Internal;
using Xunit;
using static Framework.Tests.MatchMetrics.MatchMetricsWire;
using Status = Shared.SharedMatchMetrics.MatchMetricsStatus;
using Kind = Shared.SharedMatchMetrics.MatchSeriesKind;

namespace Framework.Tests.MatchMetrics;

// The stream of the server does not stop on a series the dictionary refuses, and a write after its end is a mistake of the code.
public class MatchMetricsServerStreamFailureTests : IClassFixture<MatchMetricsDatabase>
{
    public MatchMetricsServerStreamFailureTests(MatchMetricsDatabase database)
    {
        _database = database;
    }

    private readonly MatchMetricsDatabase _database;
    private readonly Guid _match = Guid.NewGuid();

    private Task<long> Chunks()
    {
        return _database.Scalar<long>(
            "SELECT count(*) FROM match_metric_chunks t JOIN match_metric_streams s ON s.id = t.stream_id WHERE s.match_id = @m AND s.kind = 1",
            ("m", _match));
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    [Fact]
    public async Task A_gauge_of_a_name_the_dictionary_has_as_a_counter_is_dropped_and_the_rest_is_written()
    {
        var name = "players." + Guid.NewGuid().ToString("N")[..16];
        var user = Guid.NewGuid();
        _database.Admission.Add(_match, user);

        var client = _database.Connect(user);
        var stream = (await client.Open(Open(_match, Guid.NewGuid()))).StreamId;
        (await client.Accept(Batch(stream, 1, [Series(0, name, Kind.Counter)], [Chunk(0, 0, 1)]))).Status.Should().Be(Status.Ok);

        var lifetime = new Lifetime();
        var server = _database.ServerWriter(new MatchMetricsOptions { ServerFlush = TimeSpan.FromMilliseconds(100) }).Open(lifetime, _match);

        server.Gauge(name, 4);
        server.Gauge("session.users", 2);

        await TestWait.Until(async () => await Chunks() == 1);

        var again = () => server.Gauge(name, 5);
        again.Should().Throw<ArgumentException>();

        server.Gauge("session.users", 3);
        lifetime.Terminate();

        await TestWait.Until(async () => await Chunks() == 2);
    }

    [Fact]
    public async Task A_failed_last_write_is_tried_again_until_it_goes()
    {
        var db = new SwitchableDbSource(_database);
        var lifetime = new Lifetime();
        var options = new MatchMetricsOptions { ServerFlush = TimeSpan.FromHours(1), ServerLastWriteRetry = TimeSpan.FromMilliseconds(50) };
        var server = _database.ServerWriter(options, db).Open(lifetime, _match);

        server.Gauge("session.users", 2);
        db.Fail();
        lifetime.Terminate();
        await TestWait.Until(() => db.Failures >= 1);

        db.Recover();

        await TestWait.Until(async () => await Chunks() == 1);
    }

    [Fact]
    public async Task The_tries_of_the_last_write_end_after_the_close_grace()
    {
        var db = new SwitchableDbSource(_database);
        var lifetime = new Lifetime();
        var options = new MatchMetricsOptions
        {
            ServerFlush = TimeSpan.FromHours(1),
            ServerLastWriteRetry = TimeSpan.FromMilliseconds(20),
            CloseGrace = TimeSpan.FromMilliseconds(300)
        };
        var server = _database.ServerWriter(options, db).Open(lifetime, _match);

        server.Gauge("session.users", 2);
        db.Fail();
        lifetime.Terminate();
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var failures = db.Failures;
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        failures.Should().BeGreaterThan(1, "the last write is tried again");
        db.Failures.Should().Be(failures, "the tries are over");
    }

    // A gauge added while the write resolves the new series and a second over before it takes the values: it waits for the next
    // write, which finds it is a counter of the game and drops it, instead of failing every write of the stream.
    [Fact]
    public async Task A_gauge_of_a_counter_name_added_during_a_write_waits_for_the_next_one()
    {
        var counter = "players." + Guid.NewGuid().ToString("N")[..16];
        var user = Guid.NewGuid();
        _database.Admission.Add(_match, user);

        var client = _database.Connect(user);
        var stream = (await client.Open(Open(_match, Guid.NewGuid()))).StreamId;
        (await client.Accept(Batch(stream, 1, [Series(0, counter, Kind.Counter)], [Chunk(0, 0, 1)]))).Status.Should().Be(Status.Ok);

        var db = new SwitchableDbSource(_database);
        var lifetime = new Lifetime();
        var server = _database.ServerWriter(new MatchMetricsOptions { ServerFlush = TimeSpan.FromMilliseconds(100) }, db).Open(lifetime, _match);

        server.Gauge("session.users", 1);
        await TestWait.Until(async () => await Chunks() == 1);

        db.OnNextAccess(() =>
        {
            server.Gauge(counter, 4);
            Thread.Sleep(TimeSpan.FromSeconds(1.1));
        });
        server.Gauge("session." + Guid.NewGuid().ToString("N")[..16], 2);

        await TestWait.Until(async () => await Chunks() == 2);

        var again = () => server.Gauge(counter, 5);
        await TestWait.Until(() => Throws(again));
        lifetime.Terminate();
    }

    [Fact]
    public async Task A_write_after_the_end_throws()
    {
        var lifetime = new Lifetime();
        var server = _database.ServerWriter(new MatchMetricsOptions { ServerFlush = TimeSpan.FromHours(1) }).Open(lifetime, _match);

        server.Gauge("session.users", 2);
        lifetime.Terminate();
        await TestWait.Until(async () => await Chunks() == 1);

        var gauge = () => server.Gauge("session.users", 3);
        var write = () => server.Event("session.host");

        gauge.Should().Throw<InvalidOperationException>();
        write.Should().Throw<InvalidOperationException>();
    }
}
