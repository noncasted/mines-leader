using Internal;
using Shared;

namespace GamePlay.Metrics
{
    // The move snapshots of the match for the writers of the metrics, after the clock took their stamp: an event
    // written while a snapshot is handled is on the time of that snapshot.
    public interface IMatchSnapshots
    {
        IEventSource<SharedMoveSnapshot> Received { get; }
    }
}
