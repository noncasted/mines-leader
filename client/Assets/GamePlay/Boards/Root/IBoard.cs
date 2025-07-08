using System;
using System.Collections.Generic;
using System.Linq;
using Common.Network;
using Internal;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GamePlay.Boards
{
    public interface IBoard
    {
        IBoardConstructionData ConstructionDataData { get; }

        IReadOnlyDictionary<Vector2Int, IBoardCell> Cells { get; }
        bool IsMine { get; }

        IViewableDelegate Updated { get; }

        void Setup(INetworkEntity entity);
        void InvokeExplosion(IBoardCell cell);
        void InvokeUpdated();
    }

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
        
        public static HashSet<Vector2Int> NeighbourPositions(this IBoard board, Vector2Int position)
        {
            var bounds = board.GetBoardBounds();

            var neighbours = new HashSet<Vector2Int>
            {
                new(position.x - 1, position.y),
                new(position.x + 1, position.y),
                new(position.x, position.y - 1),
                new(position.x, position.y + 1),
                new(position.x - 1, position.y - 1),
                new(position.x + 1, position.y + 1),
                new(position.x - 1, position.y + 1),
                new(position.x + 1, position.y - 1)
            };

            neighbours.RemoveWhere(neighbour =>
                neighbour.x < 0 || neighbour.x > bounds.x || neighbour.y < 0 || neighbour.y > bounds.y);

            return neighbours;
        }
        
        public static void IterateNeighbours(this IBoard board, Vector2Int position, Action<Vector2Int> action)
        {
            var neighbours = board.NeighbourPositions(position);

            foreach (var neighbour in neighbours)
            {
                if (board.Cells.TryGetValue(neighbour, out var cell) == false)
                    continue;

                action(neighbour);
            }
        }

        public static Vector2Int GetBoardBounds(this IBoard board)
        {
            var maxX = 0;
            var maxY = 0;

            foreach (var (position, _) in board.Cells)
            {
                if (position.x > maxX)
                    maxX = position.x;

                if (position.y > maxY)
                    maxY = position.y;
            }

            return new Vector2Int(maxX, maxY);
        }

        public static (Vector2, Vector2) GetBoardWorldBounds(this IBoard board)
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;

            foreach (var (_, cell) in board.Cells)
            {
                var position = cell.WorldPosition;

                if (position.x < minX)
                    minX = position.x;

                if (position.x > maxX)
                    maxX = position.x;

                if (position.y < minY)
                    minY = position.y;

                if (position.y > maxY)
                    maxY = position.y;
            }

            var halfCellSize = board.ConstructionDataData.CellSize / 2;

            return (new Vector2(minX - halfCellSize, minY - halfCellSize),
                new Vector2(maxX + halfCellSize, maxY + halfCellSize));
        }

        public static Vector2Int RandomPosition(this IBoard board)
        {
            var bounds = board.GetBoardBounds();
            return new Vector2Int(Random.Range(0, bounds.x), Random.Range(0, bounds.y));
        }

        public static bool HasMinesAround(this IBoard board, Vector2Int position)
        {
            var neighbours = board.NeighbourPositions(position);

            foreach (var neighbour in neighbours)
            {
                var cell = board.Cells[neighbour];

                if (cell.State.Value.Status != CellStatus.Taken)
                    continue;

                var taken = cell.EnsureTaken();

                if (taken.HasMine.Value == true)
                    return true;
            }

            return false;
        }

        public static bool IsInside(this IBoard board, Vector2 position)
        {
            var bounds = board.GetBoardWorldBounds();

            return position.x > bounds.Item1.x && position.x < bounds.Item2.x &&
                   position.y > bounds.Item1.y && position.y < bounds.Item2.y;
        }

        public static Vector2Int WorldToBoardPosition(this IBoard board, Vector2 position)
        {
            var bounds = board.GetBoardWorldBounds();

            var local = position - bounds.Item1;

            if (local.x < 0 || local.x > bounds.Item2.x - bounds.Item1.x ||
                local.y < 0 || local.y > bounds.Item2.y - bounds.Item1.y)
            {
                return new Vector2Int(-1, -1);
            }

            var x = Mathf.FloorToInt(local.x / board.ConstructionDataData.CellSize);
            var y = Mathf.FloorToInt(local.y / board.ConstructionDataData.CellSize);

            return new Vector2Int(x, y);
        }

        public static void CleanupAround(this IReadOnlyList<IBoardCell> cells)
        {
            var board = cells.First().Source;

            var passed = new HashSet<Vector2Int>();

            foreach (var cell in cells)
                passed.Add(cell.BoardPosition);

            foreach (var cell in cells)
                Check(cell.BoardPosition);

            foreach (var target in passed)
            {
                var cell = board.Cells[target];
                cell.EnsureFree();
            }

            void Check(Vector2Int target)
            {
                var neighbours = board.NeighbourPositions(target);
                neighbours.RemoveWhere(t => passed.Contains(t));
                neighbours.RemoveWhere(t => board.Cells[t].State.Value.Status == CellStatus.Free);

                foreach (var neighbour in neighbours)
                {
                    var cell = board.Cells[neighbour];

                    if (cell.State.Value.Status != CellStatus.Taken)
                        continue;

                    var taken = cell.EnsureTaken();

                    if (taken.HasMine.Value == true)
                        return;
                }

                foreach (var neighbour in neighbours)
                {
                    passed.Add(neighbour);
                    Check(neighbour);
                }
            }
        }

        public static IReadOnlyList<IBoardCell> GetClosedShape(this IBoard board, Vector2Int start)
        {
            if (start == new Vector2Int(-1, -1))
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