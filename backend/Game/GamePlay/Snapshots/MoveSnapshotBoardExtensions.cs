using Shared;

namespace Game.GamePlay.Snapshots;

public static class MoveSnapshotBoardExtensions
{
    extension(MoveSnapshot snapshot)
    {
        public IReadOnlyList<Position> RecordReveal(IBoard board, IReadOnlyList<Position> positions)
        {
            var opened = board.Revealer.Reveal(positions, snapshot);

            foreach (var position in opened)
                snapshot.RecordCellFree(board, position);

            return opened;
        }

        public IReadOnlyList<Position> RecordReveal(IBoard board, Position position)
        {
            return snapshot.RecordReveal(board, new[] { position });
        }
    }
}