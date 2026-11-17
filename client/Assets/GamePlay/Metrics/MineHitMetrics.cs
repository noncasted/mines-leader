using GamePlay.Loop;
using Global.MatchMetrics;
using Internal;
using Shared;

namespace GamePlay.Metrics
{
    // The mines the local player blew up on its board by opening a cell: mines.hit (a shield that took the hit counts too).
    public class MineHitMetrics : IScopeSetup
    {
        public MineHitMetrics(IMatchMetricsSession session, IMatchSnapshots snapshots, IGameContext context)
        {
            _session = session;
            _snapshots = snapshots;
            _context = context;
        }

        private readonly IMatchMetricsSession _session;
        private readonly IMatchSnapshots _snapshots;
        private readonly IGameContext _context;

        private IMatchCounter _hit;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _hit = _session.Stream.Counter("mines.hit");
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
                    if (boardRecord is BoardSnapshotRecord.Explosion)
                        _hit.Add(1);
                }
            }
        }
    }
}
