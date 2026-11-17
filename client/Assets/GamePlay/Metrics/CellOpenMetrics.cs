using GamePlay.Loop;
using Global.MatchMetrics;
using Internal;
using Shared;

namespace GamePlay.Metrics
{
    // The opened cells of the board of the local player: cells.opened {source = click / cascade / card} and cascades (the
    // actions whose flood fill opened more cells). A card counts on this board whoever played it.
    public class CellOpenMetrics : IScopeSetup
    {
        public CellOpenMetrics(IMatchMetricsSession session, IMatchSnapshots snapshots, IGameContext context)
        {
            _session = session;
            _snapshots = snapshots;
            _context = context;
        }

        private readonly IMatchMetricsSession _session;
        private readonly IMatchSnapshots _snapshots;
        private readonly IGameContext _context;

        private IMatchCounter _click;
        private IMatchCounter _cascade;
        private IMatchCounter _card;
        private IMatchCounter _cascades;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var stream = _session.Stream;
            _click = stream.Counter("cells.opened", MatchTags.Of("source", "click"));
            _cascade = stream.Counter("cells.opened", MatchTags.Of("source", "cascade"));
            _card = stream.Counter("cells.opened", MatchTags.Of("source", "card"));
            _cascades = stream.Counter("cascades");

            _snapshots.Received.Advise(lifetime, OnSnapshot);
        }

        private void OnSnapshot(SharedMoveSnapshot snapshot)
        {
            foreach (var record in snapshot.Records)
            {
                switch (record)
                {
                    case SharedBoardSnapshot board when board.BoardOwnerId == _context.Self.Id:
                        OnBoard(board);
                        break;
                    case PlayerSnapshotRecord.CardUse use:
                        OnCard(use);
                        break;
                }
            }
        }

        private void OnBoard(SharedBoardSnapshot board)
        {
            var opening = BoardOpening.Of(board);

            if (opening.Total == 0)
                return;

            _click.Add(opening.Direct);

            if (opening.Cascade == 0)
                return;

            _cascade.Add(opening.Cascade);
            _cascades.Add(1);
        }

        private void OnCard(PlayerSnapshotRecord.CardUse use)
        {
            var opened = CardActionCells.Opened(use.Data, _context.Self.Id);

            if (opened > 0)
                _card.Add(opened);
        }
    }
}
