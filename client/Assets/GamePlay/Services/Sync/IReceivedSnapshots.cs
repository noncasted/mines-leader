using Internal;
using Shared;

namespace GamePlay.Services
{
    // Every move snapshot as it comes from the server, before its records are queued for the visuals: the time of the
    // arrival is the time of the action. Read-only observers (the clock and the metrics of the match) listen here; the
    // records are applied by the snapshot handlers.
    public interface IReceivedSnapshots
    {
        IEventSource<SharedMoveSnapshot> Received { get; }
    }
}
