using FluentAssertions;
using Game.MatchMetrics;
using Shared;
using Xunit;
using static Framework.Tests.MatchMetrics.MatchMetricsWire;
using Kind = Shared.SharedMatchMetrics.MatchSeriesKind;

namespace Framework.Tests.MatchMetrics;

// The client is not trusted: every broken rule rejects the whole batch.
public class MatchMetricsParserTests
{
    private static readonly Dictionary<int, Kind> _none = new();
    private static readonly Dictionary<int, Kind> _known = new() { [0] = Kind.Counter, [1] = Kind.Gauge };

    private static ParsedMatchBatch Parse(SharedMatchMetrics.Batch batch, Dictionary<int, Kind>? slots = null)
    {
        return MatchMetricsBatchParser.Parse(batch, slots ?? _none);
    }

    private static void Rejects(SharedMatchMetrics.Batch batch, Dictionary<int, Kind>? slots = null)
    {
        var parse = () => Parse(batch, slots);
        parse.Should().Throw<MatchMetricsRejectedException>();
    }

    [Fact]
    public void Tags_become_canonical_json_sorted_by_key()
    {
        var parsed = Parse(Batch(Guid.Empty, 1, [Series(0, "kills", Kind.Counter, ("target", "boss"), ("card", "12"))], [Chunk(0, 0, 1)]));

        parsed.Series[0].Key.Should().Be(new MatchSeriesKey("kills", "{\"card\":\"12\",\"target\":\"boss\"}"));
    }

    [Fact]
    public void Event_fields_become_a_json_object_of_numbers_and_texts()
    {
        var parsed = Parse(Batch(Guid.Empty, 1, events: [Event("player.death", 3, new SharedMatchMetrics.MatchEventField { Key = "enemy", Text = "bat-2" }, new SharedMatchMetrics.MatchEventField { Key = "hearts", Number = 0.5 })]));

        parsed.Events[0].Fields.Should().Be("{\"enemy\":\"bat-2\",\"hearts\":0.5}");
    }

    [Fact]
    public void Seq_below_one_is_rejected() => Rejects(Batch(Guid.Empty, 0));

    [Theory]
    [InlineData("Damage")]
    [InlineData("damage dealt")]
    [InlineData("")]
    [InlineData("a234567890123456789012345678901234567890123456789012345678901234x")]
    public void A_bad_series_name_is_rejected(string name) => Rejects(Batch(Guid.Empty, 1, [Series(0, name, Kind.Counter)]));

    [Fact]
    public void A_bad_tag_is_rejected()
    {
        Rejects(Batch(Guid.Empty, 1, [Series(0, "kills", Kind.Counter, ("Card", "1"))]));
        Rejects(Batch(Guid.Empty, 1, [Series(0, "kills", Kind.Counter, ("card", "{x}"))]));
        Rejects(Batch(Guid.Empty, 1, [Series(0, "kills", Kind.Counter, ("card", "1"), ("card", "2"))]));
        Rejects(Batch(Guid.Empty, 1, [Series(0, "kills", Kind.Counter, ("a", "1"), ("b", "1"), ("c", "1"), ("d", "1"), ("e", "1"))]));
    }

    [Fact]
    public void Slots_out_of_range_declared_twice_or_unknown_are_rejected()
    {
        Rejects(Batch(Guid.Empty, 1, [Series(SharedMatchMetricsLimits.MaxSeries, "kills", Kind.Counter)]));
        Rejects(Batch(Guid.Empty, 1, [Series(2, "kills", Kind.Counter), Series(2, "xp", Kind.Counter)]));
        Rejects(Batch(Guid.Empty, 1, [Series(0, "kills", Kind.Counter)]), _known);
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(5, 0, 1)]), _known);
    }

    [Fact]
    public void NaN_is_taken_only_in_a_gauge_and_infinity_never()
    {
        Parse(Batch(Guid.Empty, 1, chunks: [Chunk(1, 0, float.NaN, 1)]), _known).Chunks.Should().HaveCount(1);
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, 0, float.NaN)]), _known);
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(1, 0, float.PositiveInfinity)]), _known);
    }

    [Fact]
    public void Chunk_sizes_buckets_and_ages_are_limited()
    {
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, 0)]), _known);
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, -1, 1)]), _known);
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, MatchMetricsLimits.MaxBucket, 1)]), _known);
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, 0, new float[MatchMetricsLimits.MaxValues + 1])]), _known);
        Rejects(Batch(Guid.Empty, 1, chunks: Enumerable.Range(0, MatchMetricsLimits.MaxChunks + 1).Select(i => Chunk(0, i, 1)).ToArray()), _known);

        // Start + count wraps around an int to a negative end.
        Rejects(Batch(Guid.Empty, 1, chunks: [Chunk(0, int.MaxValue - 47, new float[100])]), _known);

        var old = Chunk(0, 0, 1);
        old.AgeMs = MatchMetricsLimits.MaxAgeMs + 1;
        Rejects(Batch(Guid.Empty, 1, chunks: [old]), _known);
    }

    [Fact]
    public void Events_and_their_fields_are_limited()
    {
        Rejects(Batch(Guid.Empty, 1, events: [Event("Pick", 1)]));
        Rejects(Batch(Guid.Empty, 1, events: [Event("pick", float.NaN)]));
        Rejects(Batch(Guid.Empty, 1, events: [Event("pick", 1, new SharedMatchMetrics.MatchEventField { Key = "name", Text = "Player One" })]));
        Rejects(Batch(Guid.Empty, 1, events: [Event("pick", 1, new SharedMatchMetrics.MatchEventField { Key = "x", Number = double.NaN })]));
        Rejects(Batch(Guid.Empty, 1, events: [Event("pick", 1, new SharedMatchMetrics.MatchEventField { Key = "x" }, new SharedMatchMetrics.MatchEventField { Key = "x" })]));
        Rejects(Batch(Guid.Empty, 1, events: [Event("pick", 1, Enumerable.Range(0, 9).Select(i => new SharedMatchMetrics.MatchEventField { Key = $"k{i}" }).ToArray())]));
        Rejects(Batch(Guid.Empty, 1, events: Enumerable.Range(0, MatchMetricsLimits.MaxEvents + 1).Select(_ => Event("pick", 1)).ToArray()));
    }
}
