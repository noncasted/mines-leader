using System.Collections.Generic;
using Shared;

namespace GamePlay.Metrics
{
    // The cells one action of a player opened on a board (one SharedBoardSnapshot: the open or the chord with its flood
    // fill). The server records a MinesAround only where the count is not 0, so an opened cell without one is empty, and
    // the flood fill opens exactly the neighbours of the empty cells: a cell next to an empty cell opened by the same
    // action is a cascade cell, the rest were opened directly (at least one: the clicked cell).
    public readonly struct BoardOpening
    {
        public BoardOpening(int direct, int cascade)
        {
            Direct = direct;
            Cascade = cascade;
        }

        public int Direct { get; }
        public int Cascade { get; }
        public int Total => Direct + Cascade;

        private static readonly HashSet<Position> _opened = new();
        private static readonly HashSet<Position> _counted = new();

        public static BoardOpening Of(SharedBoardSnapshot snapshot)
        {
            _opened.Clear();
            _counted.Clear();

            foreach (var record in snapshot.Records)
            {
                switch (record)
                {
                    case BoardSnapshotRecord.CellFree free:
                        _opened.Add(free.Position);
                        break;
                    case BoardSnapshotRecord.MinesAround { Count: > 0 } around:
                        _counted.Add(around.Position);
                        break;
                }
            }

            if (_opened.Count == 0)
                return default;

            var cascade = 0;

            foreach (var position in _opened)
            {
                if (HasEmptyNeighbour(position) == true)
                    cascade++;
            }

            if (cascade == _opened.Count)
                cascade--;

            return new BoardOpening(_opened.Count - cascade, cascade);
        }

        private static bool HasEmptyNeighbour(Position position)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var neighbour = new Position(position.x + x, position.y + y);

                    if (_opened.Contains(neighbour) == true && _counted.Contains(neighbour) == false)
                        return true;
                }
            }

            return false;
        }
    }
}
