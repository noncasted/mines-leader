using FluentAssertions;
using Game.MatchMetrics;
using Xunit;
using static Framework.Tests.MatchMetrics.MatchMetricsWire;
using Status = Shared.SharedMatchMetrics.MatchMetricsStatus;
using Kind = Shared.SharedMatchMetrics.MatchSeriesKind;

namespace Framework.Tests.MatchMetrics;

// A Duplicate answered to a stream that did not see the first copy stored (a reconnect opened while the copy was still being
// written, a commit of the server that threw after it went through) brings the slots that copy declared.
public class MatchMetricsDuplicateSlotsTests : IClassFixture<MatchMetricsDatabase>
{
    public MatchMetricsDuplicateSlotsTests(MatchMetricsDatabase database)
    {
        _database = database;
        _database.Admission.Add(_match, _user);
    }

    private readonly MatchMetricsDatabase _database;
    private readonly Guid _match = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _writer = Guid.NewGuid();

    [Fact]
    public async Task A_client_reopened_before_the_first_copy_was_stored_goes_on_after_the_duplicate()
    {
        var first = _database.Connect(_user);
        var stream = (await first.Open(Open(_match, _writer))).StreamId;

        var reopened = _database.Connect(_user);
        (await reopened.Open(Open(_match, _writer))).LastSeq.Should().Be(0);

        var batch = Batch(stream, 1, [Series(7, "kills", Kind.Counter)], [Chunk(7, 0, 1)]);
        (await first.Accept(batch)).Status.Should().Be(Status.Ok);

        (await reopened.Accept(batch)).Status.Should().Be(Status.Duplicate);
        (await reopened.Accept(Batch(stream, 2, chunks: [Chunk(7, 1, 2)]))).Status.Should().Be(Status.Ok);

        (await _database.Scalar<double>("SELECT sum FROM match_metric_totals WHERE stream_id = @s", ("s", stream))).Should().Be(3);
    }

    [Fact]
    public async Task A_stream_that_missed_its_own_stored_batch_does_not_declare_its_slots_again()
    {
        var now = DateTime.UtcNow;
        var stored = await _database.Streams.Open(_match, Guid.Empty, _writer, Shared.SharedMatchMetrics.MatchStreamKind.Server, now);
        var stale = await _database.Streams.Open(_match, Guid.Empty, _writer, Shared.SharedMatchMetrics.MatchStreamKind.Server, now);
        var key = new MatchSeriesKey("session.users", "{}");

        var batch = new ParsedMatchBatch(1, [new ParsedMatchSeries(0, key, Kind.Gauge)], [new ParsedMatchChunk(0, 0, 0, [2f])], []);
        (await _database.Writer.Write(stored, batch, now)).Should().Be(MatchMetricWrite.Stored);
        (await _database.Writer.Write(stale, batch, now)).Should().Be(MatchMetricWrite.Duplicate);

        stale.Slots.Should().ContainKey(0);
        stale.Kinds[0].Should().Be(Kind.Gauge);

        var next = new ParsedMatchBatch(2, [], [new ParsedMatchChunk(0, 1, 0, [3f])], []);
        (await _database.Writer.Write(stale, next, now)).Should().Be(MatchMetricWrite.Stored);
    }
}
