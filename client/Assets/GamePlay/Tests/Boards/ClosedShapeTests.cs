using System.Collections.Generic;
using System.Linq;
using GamePlay.Boards;
using NUnit.Framework;
using UnityEngine;

namespace GamePlay.Tests
{
    // Reference of GetClosedShape: from a taken start it walks the taken cells connected by the 8 directions and selects
    // those that touch a free cell.
    public class ClosedShapeTests
    {
        [Test]
        public void ClosedShape_MinusOne_Empty()
        {
            var board = new FakeBoard(new Vector2Int(5, 5), 1f, Vector2.zero);
            board.Free(new Vector2Int(2, 2));

            Assert.IsEmpty(board.GetClosedShape(new Vector2Int(-1, -1)));
        }

        [Test]
        public void ClosedShape_StartOutsideTheBoard_Empty()
        {
            var board = new FakeBoard(new Vector2Int(5, 5), 1f, Vector2.zero);
            board.Free(new Vector2Int(2, 2));

            Assert.IsEmpty(board.GetClosedShape(new Vector2Int(5, 0)));
        }

        [Test]
        public void ClosedShape_StartIsFree_Empty()
        {
            var board = new FakeBoard(new Vector2Int(5, 5), 1f, Vector2.zero);
            board.Free(new Vector2Int(2, 2));

            Assert.IsEmpty(board.GetClosedShape(new Vector2Int(2, 2)));
        }

        [Test]
        public void ClosedShape_NoFreeCells_Empty()
        {
            var board = new FakeBoard(new Vector2Int(5, 5), 1f, Vector2.zero);

            Assert.IsEmpty(board.GetClosedShape(new Vector2Int(0, 0)));
        }

        [Test]
        public void ClosedShape_OneFreeCell_ItsEightNeighbours()
        {
            var board = new FakeBoard(new Vector2Int(5, 5), 1f, Vector2.zero);
            board.Free(new Vector2Int(2, 2));

            var shape = Positions(board.GetClosedShape(new Vector2Int(0, 0)));

            CollectionAssert.AreEquivalent(new[]
            {
                new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1),
                new Vector2Int(1, 2), new Vector2Int(3, 2),
                new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3),
            }, shape);
        }

        [Test]
        public void ClosedShape_FreeColumn_OnlyTheBorderOfTheRegionOfTheStart()
        {
            var board = new FakeBoard(new Vector2Int(5, 3), 1f, Vector2.zero);
            board.Free(new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2));

            var left = Positions(board.GetClosedShape(new Vector2Int(0, 1)));
            var right = Positions(board.GetClosedShape(new Vector2Int(4, 2)));

            CollectionAssert.AreEquivalent(new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) }, left);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(3, 2) }, right);
        }

        [Test]
        public void ClosedShape_DiagonalTouch_JoinsRegionsAndCountsAsBorder()
        {
            // Taken cells T, free cells .:
            //   y=2  T . T
            //   y=1  . T .
            //   y=0  T . T
            var board = new FakeBoard(new Vector2Int(3, 3), 1f, Vector2.zero);
            board.Free(new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(2, 1), new Vector2Int(1, 2));

            var shape = Positions(board.GetClosedShape(new Vector2Int(0, 0)));

            CollectionAssert.AreEquivalent(new[]
            {
                new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(0, 2), new Vector2Int(2, 2),
            }, shape);
        }

        [Test]
        public void ClosedShape_RegionWithInnerCells_InnerCellsAreNotSelected()
        {
            // A 5 x 5 taken block in the corner of a 6 x 6 board with the last row and column free: only the cells of
            // the block next to them are selected.
            var board = new FakeBoard(new Vector2Int(6, 6), 1f, Vector2.zero);
            var free = new List<Vector2Int>();

            for (var i = 0; i < 6; i++)
            {
                free.Add(new Vector2Int(5, i));

                if (i < 5)
                    free.Add(new Vector2Int(i, 5));
            }

            board.Free(free.ToArray());

            var shape = Positions(board.GetClosedShape(new Vector2Int(0, 0)));
            var expected = new List<Vector2Int>();

            for (var i = 0; i < 5; i++)
            {
                expected.Add(new Vector2Int(4, i));

                if (i < 4)
                    expected.Add(new Vector2Int(i, 4));
            }

            CollectionAssert.AreEquivalent(expected, shape);
        }

        private static List<Vector2Int> Positions(IReadOnlyList<IBoardCell> cells)
        {
            return cells.Select(cell => cell.BoardPosition).ToList();
        }
    }
}
