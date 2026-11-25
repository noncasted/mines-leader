using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardConstructionData
    {
        Vector2Int Size { get; }
        float CellSize { get; }

        // The rectangle of the cells in the local space of the board: cell (0, 0) starts at its min corner.
        Rect LocalBounds { get; }

        // The centre of the cell at position in the local space of the board.
        Vector2 GetLocalPosition(Vector2Int position);
    }

    // The layout of the cells of a board, fixed at its creation: the cells abut each other around the origin of the
    // board, the centre of cell (0, 0) at the origin of the layout.
    public class BoardConstructionData : IBoardConstructionData
    {
        public BoardConstructionData(Vector2Int size, float cellSize)
        {
            Size = size;
            CellSize = cellSize;

            var origin = new Vector2(size.x * cellSize / 2, size.y * cellSize / 2) * -1f;

            if (size.x % 2 == 0)
                origin.x += cellSize / 2;

            if (size.y % 2 == 0)
                origin.y += cellSize / 2;

            _origin = origin;

            var halfCell = new Vector2(cellSize / 2, cellSize / 2);
            LocalBounds = new Rect(origin - halfCell, new Vector2(size.x * cellSize, size.y * cellSize));
        }

        private readonly Vector2 _origin;

        public Vector2Int Size { get; }
        public float CellSize { get; }
        public Rect LocalBounds { get; }

        public Vector2 GetLocalPosition(Vector2Int position)
        {
            return new Vector2(
                position.x * CellSize + _origin.x,
                position.y * CellSize + _origin.y);
        }
    }
}
