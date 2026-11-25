using System;
using System.Collections.Generic;
using Internal;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoard
    {
        IBoardConstructionData ConstructionDataData { get; }
        IViewableProperty<BoardState> State { get; }

        IReadOnlyDictionary<Vector2Int, IBoardCell> Cells { get; }
        bool IsMine { get; }

        // A point of the world in the local space of the board, where ConstructionDataData lays the cells out: the
        // bounds are fixed after the build and follow a move of the board.
        Vector2 WorldToLocal(Vector2 world);

        /// <summary>
        /// The board is generated on the server - it does not exist until the owner's first move,
        /// and cards cannot be played on it.
        /// </summary>
        IViewableProperty<bool> IsGenerated { get; }

        void Setup(bool isMine);
        void OnGenerated();
        void UpdateState(int mines, int flags);
    }

    [NoAutoStaticsCleanup]
    public static class BoardExtensions
    {
        public static readonly IReadOnlyList<Vector2Int> Directions = new List<Vector2Int>()
        {
            new(0, 1),
            new(1, 1),
            new(1, 0),
            new(1, -1),
            new(0, -1),
            new(-1, -1),
            new(-1, 0),
            new(-1, 1),
        };

        // The board position of a point outside the board.
        public static readonly Vector2Int Outside = new(-1, -1);

        public static bool IsInside(this IBoard board, Vector2 world)
        {
            var bounds = board.ConstructionDataData.LocalBounds;
            var local = board.WorldToLocal(world);

            return local.x > bounds.xMin &&
                   local.x < bounds.xMax &&
                   local.y > bounds.yMin &&
                   local.y < bounds.yMax;
        }

        public static Vector2Int WorldToBoardPosition(this IBoard board, Vector2 world)
        {
            var data = board.ConstructionDataData;
            var bounds = data.LocalBounds;
            var local = board.WorldToLocal(world) - bounds.min;

            // A cell spans [start, start + size): the upper and right edges of the board belong to no cell, like
            // every other point outside. Cells abut each other (BoardConstructor has no gap between them), so the
            // position is the local point divided by the size of a cell.
            if (local.x < 0 ||
                local.x >= bounds.width ||
                local.y < 0 ||
                local.y >= bounds.height)
            {
                return Outside;
            }

            var x = Mathf.FloorToInt(local.x / data.CellSize);
            var y = Mathf.FloorToInt(local.y / data.CellSize);

            return new Vector2Int(x, y);
        }

        // The cell under a point of the world, found from the coordinates: every position inside the bounds has a cell.
        public static bool TryGetCellAt(this IBoard board, Vector2 world, out IBoardCell cell)
        {
            var position = board.WorldToBoardPosition(world);

            if (position == Outside)
            {
                cell = null;
                return false;
            }

            cell = board.Cells[position];
            return true;
        }

        // The cell of a position the server sent for the board of owner: the server board has the same cells, so a
        // position without a cell is a broken sync.
        public static IBoardCell GetSyncedCell(this IBoard board, Guid owner, Position position)
        {
            if (board.Cells.TryGetValue(position.ToVector(), out var cell) == false)
            {
                throw new InvalidOperationException(
                    $"Cell ({position.x}, {position.y}) is not on the board of player {owner} " +
                    $"(size {board.ConstructionDataData.Size})");
            }

            return cell;
        }

        public static IReadOnlyList<IBoardCell> GetClosedShape(this IBoard board, Vector2Int start)
        {
            if (start == Outside)
                return Array.Empty<IBoardCell>();

            var cells = board.Cells;

            var selected = new HashSet<Vector2Int>();
            var checkedCache = new HashSet<Vector2Int>();

            Check(start);

            var result = new List<IBoardCell>();

            foreach (var position in selected)
            {
                if (cells.TryGetValue(position, out var cell) == false)
                    throw new KeyNotFoundException();

                result.Add(cell);
            }

            return result;

            bool Check(Vector2Int position)
            {
                if (cells.TryGetValue(position, out var cell) == false)
                    return false;

                if (cell.State.Value.Status == CellStatus.Free)
                    return true;

                if (checkedCache.Contains(position) == true)
                    return false;

                checkedCache.Add(position);

                foreach (var direction in Directions)
                {
                    var next = position + direction;

                    if (Check(next) == true)
                        selected.Add(position);
                }

                return false;
            }
        }
    }
}