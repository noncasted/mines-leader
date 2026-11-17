using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Metrics
{
    // Feeds the clock first and the writers next with every snapshot the client receives. The completion of the match
    // stops the clock before the writers see it, so match.end and the last turn.end stand on the time of the completion.
    public class MatchSnapshots : IMatchSnapshots, IScopeSetup
    {
        public MatchSnapshots(IReceivedSnapshots snapshots, MatchClock clock)
        {
            _snapshots = snapshots;
            _clock = clock;
        }

        private readonly IReceivedSnapshots _snapshots;
        private readonly MatchClock _clock;
        private readonly EventSource<SharedMoveSnapshot> _received = new();

        public IEventSource<SharedMoveSnapshot> Received => _received;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _snapshots.Received.Advise(lifetime, OnReceived);
        }

        private void OnReceived(SharedMoveSnapshot snapshot)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            _clock.Receive(snapshot.MatchMs, now);

            foreach (var record in snapshot.Records)
            {
                if (record is GameCompletedRecord)
                    _clock.Freeze(now);
            }

            _received.Invoke(snapshot);
        }
    }
}
