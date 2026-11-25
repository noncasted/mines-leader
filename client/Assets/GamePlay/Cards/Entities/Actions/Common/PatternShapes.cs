using System;
using System.Collections.Generic;
using GamePlay.Boards;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IPattenShape
    {
        IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
    }

    public static class PatternShapes
    {
        public static RhombusShape Rhombus(int size) => new(size);
        public static LineShape Line(int length, bool horizontal) => new(length, horizontal);
        public static CrossShape Cross(int size) => new(size);

        // The selections add the cells of the shape centred on center to result: the caller owns and clears the
        // buffer, so aiming allocates nothing per frame.
        public static void SelectTaken(
            this IPattenShape shape,
            IBoard board,
            Vector2Int center,
            List<IBoardCell> result)
        {
            shape.Select(board, center, cell => cell.IsTaken(), result);
        }

        public static void SelectFree(
            this IPattenShape shape,
            IBoard board,
            Vector2Int center,
            List<IBoardCell> result)
        {
            shape.Select(board, center, cell => cell.IsFree(), result);
        }

        public static void All(this IPattenShape shape, IBoard board, Vector2Int center, List<IBoardCell> result)
        {
            shape.Select(board, center, _ => true, result);
        }

        public static void Select(
            this IPattenShape shape,
            IBoard board,
            Vector2Int center,
            Func<IBoardCell, bool> filter,
            List<IBoardCell> result)
        {
            if (center == BoardExtensions.Outside)
                return;

            var size = shape.Positions.Count;
            var halfSize = size / 2;
            var start = center - new Vector2Int(halfSize, halfSize);
            var cells = board.Cells;

            for (var y = 0; y < size; y++)
            {
                var row = shape.Positions[y];

                for (var x = 0; x < size; x++)
                {
                    if (row[x] == false)
                        continue;

                    if (cells.TryGetValue(start + new Vector2Int(x, y), out var cell) == false)
                        continue;

                    if (filter(cell) == false)
                        continue;

                    result.Add(cell);
                }
            }
        }
    }

    public class RhombusShape : IPattenShape
    {
        public RhombusShape(int size)
        {
            var isEven = size % 2 == 0;

            if (size % 2 == 0)
                size += 2;

            var positions = new bool[size][];
            var center = (size - 1) / 2.0;

            for (var i = 0; i < size; i++)
            {
                positions[i] = new bool[size];

                for (var j = 0; j < size; j++)
                {
                    var distance = Math.Abs(i - center) + Math.Abs(j - center);
                    positions[i][j] = distance <= center;
                }
            }

            if (isEven)
            {
                for (var i = 0; i < size; i++)
                {
                    var currentRow = positions[i];
                    var newRow = new bool[size - 2];

                    for (var j = 0; j < size - 2; j++)
                        newRow[j] = currentRow[j + 1];

                    positions[i] = newRow;
                }

                var newPositions = new bool[size - 2][];

                for (var i = 0; i < size - 2; i++)
                    newPositions[i] = positions[i + 1];

                positions = newPositions;
            }

            Positions = positions;
        }

        public IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
    }

    public class LineShape : IPattenShape
    {
        public LineShape(int length, bool horizontal)
        {
            var grid = new bool[length][];
            var center = length / 2;

            for (var y = 0; y < length; y++)
            {
                grid[y] = new bool[length];

                for (var x = 0; x < length; x++)
                {
                    grid[y][x] = horizontal ? y == center : x == center;
                }
            }

            Positions = grid;
        }

        public IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
    }

    public class CrossShape : IPattenShape
    {
        public CrossShape(int size)
        {
            size = 2 * size - 1;

            var grid = new bool[size][];
            var center = size / 2;

            for (var y = 0; y < size; y++)
            {
                grid[y] = new bool[size];

                for (var x = 0; x < size; x++)
                {
                    grid[y][x] = x == center || y == center;
                }
            }

            Positions = grid;
        }

        public IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
    }
}