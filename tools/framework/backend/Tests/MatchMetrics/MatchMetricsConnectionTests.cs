using FluentAssertions;
using Internal;
using Shared;
using Xunit;
using static Framework.Tests.MatchMetrics.MatchMetricsWire;
using Status = Shared.SharedMatchMetrics.MatchMetricsStatus;
using Kind = Shared.SharedMatchMetrics.MatchSeriesKind;

namespace Framework.Tests.MatchMetrics;

// The streams of a socket over the real tables, without a socket: its lifetime and the user of its session are enough.
public class MatchMetricsConnectionTests : IClassFixture<MatchMetricsDatabase>
{
    public MatchMetricsConnectionTests(MatchMetricsDatabase database)
    {
        _database = database;
        _database.Admission.Add(_match, _user);
    }

    private readonly MatchMetricsDatabase _database;
    private readonly Guid _match = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _writer = Guid.NewGuid();

    private static readonly SharedMatchMetrics.MatchSeriesDecl _damage = Series(0, "damage.dealt", Kind.Counter, ("source", "shot"), ("target", "regular"));
    private static readonly SharedMatchMetrics.MatchSeriesDecl _hearts = Series(1, "hearts", Kind.Gauge);

    private async Task<Guid> OpenStream()
    {
        var opened = await _database.Connect(_user).Open(Open(_match, _writer));
        opened.Status.Should().Be(Status.Ok);
        return opened.StreamId;
    }

    [Fact]
    public async Task A_batch_is_stored_with_its_series_chunks_events_and_totals()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;

        var result = await connection.Accept(Batch(stream, 1,
            [_damage, _hearts],
            [Chunk(0, 0, 10, 20, 30), Chunk(1, 0, 3, float.NaN, 2), Chunk(0, 5, 5)],
            [Event("upgrade.pick", 2.5f, new SharedMatchMetrics.MatchEventField { Key = "card", Number = 12 })]));

        result.Status.Should().Be(Status.Ok);

        (await _database.Scalar<long>("SELECT count(*) FROM match_metric_chunks WHERE stream_id = @s", ("s", stream))).Should().Be(3);
        (await _database.Scalar<string>("SELECT fields::text FROM match_metric_events WHERE stream_id = @s", ("s", stream))).Should().Be("{\"card\": 12}");
        (await _database.Scalar<int>("SELECT last_seq FROM match_metric_streams WHERE id = @s", ("s", stream))).Should().Be(1);
        (await _database.Scalar<Guid>("SELECT user_id FROM match_metric_streams WHERE id = @s", ("s", stream))).Should().Be(_user);
    }

    [Fact]
    public async Task Totals_are_the_sum_of_the_chunks_over_batches()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;

        await connection.Accept(Batch(stream, 1, [_damage, _hearts], [Chunk(0, 0, 10, 20), Chunk(1, 0, 3, float.NaN)]));
        await connection.Accept(Batch(stream, 2, chunks: [Chunk(0, 2, 5, 0, 7), Chunk(1, 2, 1)]));

        const string chunks = """
                              SELECT sum(v)::float8 FROM match_metric_chunks c
                              JOIN match_metric_series s ON s.id = c.series_id
                              CROSS JOIN LATERAL unnest(c.values) v
                              WHERE c.stream_id = @s AND s.name = @n AND v <> 'NaN'
                              """;
        const string totals = """
                              SELECT t.sum FROM match_metric_totals t JOIN match_metric_series s ON s.id = t.series_id
                              WHERE t.stream_id = @s AND s.name = @n
                              """;

        foreach (var name in new[] { "damage.dealt", "hearts" })
        {
            var sum = await _database.Scalar<double>(chunks, ("s", stream), ("n", name));
            (await _database.Scalar<double>(totals, ("s", stream), ("n", name))).Should().Be(sum);
        }

        (await _database.Scalar<double>(totals, ("s", stream), ("n", "damage.dealt"))).Should().Be(42);
        (await _database.Scalar<int>("SELECT t.count FROM match_metric_totals t JOIN match_metric_series s ON s.id = t.series_id WHERE t.stream_id = @s AND s.name = 'hearts'", ("s", stream)))
            .Should().Be(2);
    }

    [Fact]
    public async Task A_repeated_seq_is_a_duplicate_and_writes_nothing()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;
        var batch = Batch(stream, 1, [_damage], [Chunk(0, 0, 10)]);

        (await connection.Accept(batch)).Status.Should().Be(Status.Ok);
        (await connection.Accept(batch)).Status.Should().Be(Status.Duplicate);

        (await _database.Scalar<long>("SELECT count(*) FROM match_metric_chunks WHERE stream_id = @s", ("s", stream))).Should().Be(1);
        (await _database.Scalar<double>("SELECT sum FROM match_metric_totals WHERE stream_id = @s", ("s", stream))).Should().Be(10);
    }

    [Fact]
    public async Task A_reconnect_finds_the_stream_its_last_seq_and_its_slots()
    {
        var first = new Lifetime();
        var connection = _database.Connect(_user, first);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;
        (await connection.Accept(Batch(stream, 1, [_damage], [Chunk(0, 0, 10)]))).Status.Should().Be(Status.Ok);
        first.Terminate();

        var again = _database.Connect(_user);
        var opened = await again.Open(Open(_match, _writer));

        opened.StreamId.Should().Be(stream);
        opened.LastSeq.Should().Be(1);

        (await again.Accept(Batch(stream, 1, [_damage], [Chunk(0, 0, 10)]))).Status.Should().Be(Status.Duplicate);
        (await again.Accept(Batch(stream, 2, chunks: [Chunk(0, 1, 4)]))).Status.Should().Be(Status.Ok);
        (await _database.Scalar<double>("SELECT sum FROM match_metric_totals WHERE stream_id = @s", ("s", stream))).Should().Be(14);
    }

    [Fact]
    public async Task A_gap_in_seq_is_rejected()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;

        (await connection.Accept(Batch(stream, 2, [_damage], [Chunk(0, 0, 10)]))).Status.Should().Be(Status.Rejected);
        (await _database.Scalar<int>("SELECT last_seq FROM match_metric_streams WHERE id = @s", ("s", stream))).Should().Be(0);
    }

    [Fact]
    public async Task A_user_not_of_the_match_is_not_in_match()
    {
        var stranger = await _database.Connect(Guid.NewGuid()).Open(Open(_match, _writer));
        var unknown = await _database.Connect(_user).Open(Open(Guid.NewGuid(), _writer));

        stranger.Status.Should().Be(Status.NotInMatch);
        unknown.Status.Should().Be(Status.NotInMatch);
    }

    [Fact]
    public async Task A_stream_of_another_socket_or_a_server_kind_is_rejected()
    {
        var stream = await OpenStream();

        (await _database.Connect(_user).Accept(Batch(stream, 1, [_damage], [Chunk(0, 0, 1)]))).Status.Should().Be(Status.Rejected);

        var server = Open(_match, _writer);
        server.Kind = SharedMatchMetrics.MatchStreamKind.Server;
        (await _database.Connect(_user).Open(server)).Status.Should().Be(Status.Rejected);
    }

    [Fact]
    public async Task A_closed_match_takes_batches_in_its_grace_then_is_closed()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;
        _database.Admission.Close(_match, _database.Time.GetUtcNow().UtcDateTime);

        _database.Time.Advance(TimeSpan.FromSeconds(61));
        (await connection.Accept(Batch(stream, 1, [_damage], [Chunk(0, 0, 1)]))).Status.Should().Be(Status.Ok);

        _database.Time.Advance(TimeSpan.FromMinutes(2));
        (await connection.Accept(Batch(stream, 2, chunks: [Chunk(0, 1, 1)]))).Status.Should().Be(Status.Closed);
        (await _database.Connect(_user).Open(Open(_match, _writer))).Status.Should().Be(Status.Closed);
    }

    [Fact]
    public async Task The_admission_is_asked_on_open_and_again_only_after_the_recheck()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, _writer))).StreamId;
        var checks = _database.Admission.Checks;

        await connection.Accept(Batch(stream, 1, [_damage], [Chunk(0, 0, 1)]));
        await connection.Accept(Batch(stream, 2, chunks: [Chunk(0, 1, 1)]));
        _database.Admission.Checks.Should().Be(checks);

        _database.Time.Advance(TimeSpan.FromSeconds(61));
        await connection.Accept(Batch(stream, 3, chunks: [Chunk(0, 2, 1)]));
        _database.Admission.Checks.Should().Be(checks + 1);
    }

    [Fact]
    public async Task A_series_of_the_same_name_and_tags_is_one_row_of_the_dictionary()
    {
        var other = Guid.NewGuid();
        _database.Admission.Add(_match, _user, other);

        foreach (var user in new[] { _user, other })
        {
            var connection = _database.Connect(user);
            var stream = (await connection.Open(Open(_match, Guid.NewGuid()))).StreamId;
            var reordered = Series(3, "damage.dealt", Kind.Counter, ("target", "regular"), ("source", "shot"));
            (await connection.Accept(Batch(stream, 1, [user == _user ? _damage : reordered], [Chunk(user == _user ? 0 : 3, 0, 1)]))).Status.Should().Be(Status.Ok);
        }

        (await _database.Scalar<long>("SELECT count(DISTINCT t.series_id) FROM match_metric_totals t JOIN match_metric_streams s ON s.id = t.stream_id WHERE s.match_id = @m", ("m", _match)))
            .Should().Be(1);
    }
}
