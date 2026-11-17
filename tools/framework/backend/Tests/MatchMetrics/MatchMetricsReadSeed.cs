using FluentAssertions;
using Shared;
using static Framework.Tests.MatchMetrics.MatchMetricsWire;
using Kind = Shared.SharedMatchMetrics.MatchSeriesKind;
using Status = Shared.SharedMatchMetrics.MatchMetricsStatus;

namespace Framework.Tests.MatchMetrics;

// One match of two players for the reading: damage by source and card, a gauge of hearts, two events.
// A: shot 10 20 30 .. at 12: 7, bolt/lightning 5 5, hearts 3 NaN 2, upgrade.pick at 2.5. B: shot 1 1, player.death at 8.
public sealed class MatchMetricsReadSeed
{
    public Guid Match { get; } = Guid.NewGuid();
    public Guid A { get; } = Guid.NewGuid();
    public Guid B { get; } = Guid.NewGuid();

    public async Task Write(MatchMetricsDatabase database)
    {
        database.Admission.Add(Match, A, B);

        var shot = Series(0, "damage.dealt", Kind.Counter, ("source", "shot"));
        var bolt = Series(1, "damage.dealt", Kind.Counter, ("source", "bolt"), ("card", "lightning"));
        var hearts = Series(2, "hearts", Kind.Gauge);
        var pick = Event("upgrade.pick", 2.5f, new SharedMatchMetrics.MatchEventField { Key = "card", Number = 12 });

        var a = database.Connect(A);
        var aStream = (await a.Open(Open(Match, Guid.NewGuid()))).StreamId;
        await Stored(a.Accept(Batch(aStream, 1, [shot, bolt, hearts], [Chunk(0, 0, 10, 20, 30), Chunk(1, 0, 5, 5), Chunk(2, 0, 3, float.NaN, 2)], [pick])));
        await Stored(a.Accept(Batch(aStream, 2, chunks: [Chunk(0, 12, 7)])));

        var b = database.Connect(B);
        var bStream = (await b.Open(Open(Match, Guid.NewGuid()))).StreamId;
        await Stored(b.Accept(Batch(bStream, 1, [shot], [Chunk(0, 0, 1, 1)], [Event("player.death", 8)])));
    }

    private static async Task Stored(Task<SharedMatchMetrics.BatchResult> result)
    {
        (await result).Status.Should().Be(Status.Ok);
    }
}
