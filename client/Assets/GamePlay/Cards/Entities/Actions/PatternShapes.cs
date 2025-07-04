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

                    if (cell.State.Value.Status != CellStatus.Free)
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
            var positions = new bool[size][];
            var offsets = new int[size];
            var startOffset = size / 2;
            var currentOffset = startOffset;
            var middlePoint = Mathf.FloorToInt((size - 1) / 2f);

            for (var i = 0; i < size; i++)
            {
                offsets[i] = currentOffset;

                if (i < middlePoint)
                {
                    currentOffset--;
                }
                else if (i >= middlePoint)
                {
                    currentOffset++;
                }
            }

            for (var i = 0; i < size; i++)
            {
                positions[i] = new bool[size];
                var offset = offsets[i];

                for (var j = 0; j < size; j++)
                {
                    if (j < offset || j >= size - offset)
                    {
                        positions[i][j] = false;
                    }
                    else
                    {
                        positions[i][j] = true;
                    }
                }
            }

            Positions = positions;
        }

        public IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
    }
}