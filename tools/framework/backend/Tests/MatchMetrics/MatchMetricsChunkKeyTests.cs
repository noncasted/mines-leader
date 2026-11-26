using FluentAssertions;
using Game.MatchMetrics;
using Shared;
using Xunit;
using static Framework.Tests.MatchMetrics.MatchMetricsWire;
using Status = Shared.SharedMatchMetrics.MatchMetricsStatus;
using Kind = Shared.SharedMatchMetrics.MatchSeriesKind;

namespace Framework.Tests.MatchMetrics;

// Two chunks of one series in one batch must not share a bucket: the key of a chunk is (stream, seq, series, start), and a
// bucket counted twice breaks the totals. Such a batch is Rejected, not a failure of the database repeated forever.
public class MatchMetricsChunkKeyTests : IClassFixture<MatchMetricsDatabase>
{
    public MatchMetricsChunkKeyTests(MatchMetricsDatabase database)
    {
        _database = database;
        _database.Admission.Add(_match, _user);
    }

    private static readonly Dictionary<int, Kind> _known = new() { [0] = Kind.Counter };

    private readonly MatchMetricsDatabase _database;
    private readonly Guid _match = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    private static void Rejects(SharedMatchMetrics.Batch batch)
    {
        var parse = () => MatchMetricsBatchParser.Parse(batch, _known);
        parse.Should().Throw<MatchMetricsRejectedException>();
    }

    [Fact]
    public void Chunks_of_one_slot_with_the_same_start_are_rejected() => Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, 3, 1), Chunk(0, 3, 2)]));

    [Fact]
    public void Overlapping_chunks_of_one_slot_are_rejected() => Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, 5, 1), Chunk(0, 2, 1, 2, 3, 4)]));

    [Fact]
    public void Adjacent_chunks_of_one_slot_pass()
    {
        var parsed = MatchMetricsBatchParser.Parse(Batch(Guid.Empty, 1, chunks: [Chunk(0, 2, 1, 2), Chunk(0, 4, 3)]), _known);
        parsed.Chunks.Should().HaveCount(2);
    }

    [Fact]
    public void One_series_declared_in_two_slots_of_a_batch_is_rejected()
    {
        Rejects(Batch(Guid.Empty, 1, [Series(1, "kills", Kind.Counter, ("a", "1"), ("b", "2")), Series(2, "kills", Kind.Counter, ("b", "2"), ("a", "1"))]));
    }

    [Fact]
    public async Task A_series_declared_again_in_another_slot_of_the_stream_is_rejected()
    {
        var connection = _database.Connect(_user);
        var stream = (await connection.Open(Open(_match, Guid.NewGuid()))).StreamId;

        (await connection.Accept(Batch(stream, 1, [Series(0, "kills", Kind.Counter)], [Chunk(0, 0, 1)]))).Status.Should().Be(Status.Ok);
        (await connection.Accept(Batch(stream, 2, [Series(1, "kills", Kind.Counter)], [Chunk(0, 1, 1), Chunk(1, 1, 1)]))).Status.Should().Be(Status.Rejected);

        (await _database.Scalar<int>("SELECT last_seq FROM match_metric_streams WHERE id = @s", ("s", stream))).Should().Be(1);
    }
}
