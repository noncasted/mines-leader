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

        public static IReadOnlyList<IBoardCell> SelectTaken(this IPattenShape shape, IBoard board, Vector2Int center)
        {
            return shape.Select(board, center, cell => cell.IsTaken());
        }
        
        public static IReadOnlyList<IBoardCell> SelectFree(this IPattenShape shape, IBoard board, Vector2Int center)
        {
            return shape.Select(board, center, cell => cell.IsFree());
        }

        public static IReadOnlyList<IBoardCell> Select(
            this IPattenShape shape,
            IBoard board,
            Vector2Int center,
            Func<IBoardCell, bool> filter)
        {
            if (center == new Vector2Int(-1, -1))
                return Array.Empty<IBoardCell>();

            var size = shape.Positions.Count;
            var halfSize = size / 2;
            var start = center - new Vector2Int(halfSize, halfSize);

            var selected = new List<IBoardCell>();

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (shape.Positions[y][x] == false)
                        continue;

                    var position = start + new Vector2Int(x, y);

                    if (board.Cells.TryGetValue(position, out var cell) == false)
                        continue;

                    if (filter(cell) == false)
                        continue;

                    selected.Add(board.Cells[position]);
                }
            }

            return selected;
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
}