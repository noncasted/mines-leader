using Shared;

namespace Game.MatchMetrics;

// A stream as its writer sees it: the last batch stored and the slots of its series. Changed only by the writes of its
// batches, one at a time (one batch in flight per stream).
internal sealed class MatchMetricStream
{
    public MatchMetricStream(Guid id, Guid matchId, int lastSeq, IReadOnlyDictionary<int, MatchMetricSlot> slots)
    {
        Id = id;
        MatchId = matchId;
        LastSeq = lastSeq;

        foreach (var (slot, value) in slots)
        {
            _slots.Add(slot, value);
            _kinds.Add(slot, value.Kind);
        }
    }

    private readonly Dictionary<int, MatchMetricSlot> _slots = new();
    private readonly Dictionary<int, SharedMatchMetrics.MatchSeriesKind> _kinds = new();

    public Guid Id { get; }
    public Guid MatchId { get; }
    public int LastSeq { get; private set; }

    public IReadOnlyDictionary<int, MatchMetricSlot> Slots => _slots;
    public IReadOnlyDictionary<int, SharedMatchMetrics.MatchSeriesKind> Kinds => _kinds;

    public void OnStored(int seq, IReadOnlyDictionary<int, MatchMetricSlot> added)
    {
        LastSeq = seq;

        foreach (var (slot, value) in added)
        {
            _slots.Add(slot, value);
            _kinds.Add(slot, value.Kind);
        }
    }

    // A copy stored before: the server is at least at seq, and the slots that copy declared are the stream's.
    public void OnDuplicate(int lastSeq, IReadOnlyDictionary<int, MatchMetricSlot> stored)
    {
        LastSeq = Math.Max(LastSeq, lastSeq);

        foreach (var (slot, value) in stored)
        {
            if (_slots.TryAdd(slot, value))
                _kinds.Add(slot, value.Kind);
        }
    }
}
