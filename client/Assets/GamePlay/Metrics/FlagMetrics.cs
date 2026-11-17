using GamePlay.Loop;
using Global.MatchMetrics;
using Internal;
using Shared;

namespace GamePlay.Metrics
{
    // The flags the local player set and removed on its board: flags.set, flags.removed.
    public class FlagMetrics : IScopeSetup
    {
        public FlagMetrics(IMatchMetricsSession session, IMatchSnapshots snapshots, IGameContext context)
        {
            _session = session;
            _snapshots = snapshots;
            _context = context;
        }

        private readonly IMatchMetricsSession _session;
        private readonly IMatchSnapshots _snapshots;
        private readonly IGameContext _context;

        private IMatchCounter _set;
        private IMatchCounter _removed;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _set = _session.Stream.Counter("flags.set");
            _removed = _session.Stream.Counter("flags.removed");

            _snapshots.Received.Advise(lifetime, OnSnapshot);
        }

        private void OnSnapshot(SharedMoveSnapshot snapshot)
        {
            foreach (var record in snapshot.Records)
            {
                if (record is not SharedBoardSnapshot board || board.BoardOwnerId != _context.Self.Id)
                    continue;

                foreach (var boardRecord in board.Records)
                {
                    if (boardRecord is not BoardSnapshotRecord.Flag flag)
                        continue;

                    if (flag.IsFlagged == true)
                        _set.Add(1);
                    else
                        _removed.Add(1);
                }
            }
        }
    }
}
