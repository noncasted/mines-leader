using GamePlay.Boards;
using NUnit.Framework;
using UnityEngine;

namespace GamePlay.Tests
{
    // Reference of BoardExtensions taken before board_cells_performance and kept since: IsInside is strict,
    // WorldToBoardPosition counts cells from the lower left corner of the bounds and, since error_handling_consistency,
    // gives no position on the upper and right edges. Since board_cells_performance the bounds are the layout of
    // BoardConstructionData in the local space of the board, computed once, not from the cells on every call.
    public class BoardGeometryTests
    {
        private static readonly Vector2Int _outside = new(-1, -1);

        [Test]
        public void LocalBounds_MatchBoard_HalfACellAroundTheCentresOfTheCells()
        {
            var data = new BoardConstructionData(new Vector2Int(16, 16), 1f);

            Assert.AreEqual(new Vector2(-8f, -8f), data.LocalBounds.min);
            Assert.AreEqual(new Vector2(8f, 8f), data.LocalBounds.max);
            Assert.AreEqual(new Vector2(-7.5f, -7.5f), data.GetLocalPosition(new Vector2Int(0, 0)));
            Assert.AreEqual(new Vector2(7.5f, 7.5f), data.GetLocalPosition(new Vector2Int(15, 15)));
        }

        // The layout of BoardConstructor: an odd side is shifted by half a cell from the centre of the board.
        [Test]
        public void LocalBounds_OddSideWithSmallCells_TheLayoutOfTheConstructor()
        {
            var data = new BoardConstructionData(new Vector2Int(4, 3), 0.5f);

            Assert.AreEqual(new Vector2(-1f, -1f), data.LocalBounds.min);
            Assert.AreEqual(new Vector2(1f, 0.5f), data.LocalBounds.max);
            Assert.AreEqual(new Vector2(-0.75f, -0.75f), data.GetLocalPosition(new Vector2Int(0, 0)));
        }

        [Test]
        public void IsInside_ShiftedBoardWithSmallCells_TheBoundsOfItsCells()
        {
            var board = new FakeBoard(new Vector2Int(4, 3), 0.5f, new Vector2(2.5f, -3.5f));

            Assert.IsTrue(board.IsInside(new Vector2(2.26f, -3.74f)));
            Assert.IsTrue(board.IsInside(new Vector2(4.24f, -2.26f)));
            Assert.IsFalse(board.IsInside(new Vector2(2.25f, -3f)));
            Assert.IsFalse(board.IsInside(new Vector2(4.25f, -3f)));
            Assert.IsFalse(board.IsInside(new Vector2(3f, -3.75f)));
            Assert.IsFalse(board.IsInside(new Vector2(3f, -2.25f)));
        }

        // The bounds are kept in the local space of the board: a moved board is found where it is now.
        [Test]
        public void MovedBoard_PositionsFollowTheBoard()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));
            var cell = board.Cell(3, 5);

            board.MoveTo(new Vector2(20f, -4f));

            Assert.IsFalse(board.IsInside(new Vector2(0f, 0f)));
            Assert.IsTrue(board.IsInside(new Vector2(20f, -4f)));
            Assert.AreEqual(new Vector2Int(3, 5), board.WorldToBoardPosition(cell.WorldPosition));
            Assert.AreEqual(new Vector2(15.5f, -6.5f), cell.WorldPosition);
        }

        [Test]
        public void TryGetCellAt_CentreOfEveryCell_ThatCell()
        {
            var board = FakeBoard.Match(new Vector2(10f, 2f));

            foreach (var (position, cell) in board.Cells)
            {
                Assert.IsTrue(board.TryGetCellAt(cell.WorldPosition, out var found), $"cell {position}");
                Assert.AreSame(cell, found, $"cell {position}");
            }
        }

        [Test]
        public void TryGetCellAt_OutsideAndOnTheUpperRightEdge_NoCell()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            Assert.IsFalse(board.TryGetCellAt(new Vector2(8.01f, 0f), out var outside));
            Assert.IsNull(outside);
            Assert.IsFalse(board.TryGetCellAt(new Vector2(8f, 8f), out _));
            Assert.IsTrue(board.TryGetCellAt(new Vector2(-8f, -8f), out var corner));
            Assert.AreSame(board.Cell(0, 0), corner);
        }

        // Two boards of a match: the cell under the pointer is on the board the pointer is over.
        [Test]
        public void TryGetCellAt_TwoBoards_OnlyTheBoardUnderThePointer()
        {
            var own = FakeBoard.Match(new Vector2(0f, -9f));
            var other = FakeBoard.Match(new Vector2(0f, 9f));
            var point = new Vector2(3.3f, 5f);

            Assert.IsFalse(own.TryGetCellAt(point, out _));
            Assert.IsTrue(other.TryGetCellAt(point, out var cell));
            Assert.AreEqual(new Vector2Int(11, 4), cell.BoardPosition);
        }

        [Test]
        public void IsInside_CentreAndCornerCells_True()
        {
            var board = FakeBoard.Match(new Vector2(10f, 2f));

            Assert.IsTrue(board.IsInside(new Vector2(10f, 2f)));
            Assert.IsTrue(board.IsInside(board.Cell(0, 0).WorldPosition));
            Assert.IsTrue(board.IsInside(board.Cell(15, 15).WorldPosition));
            Assert.IsTrue(board.IsInside(new Vector2(2.01f, -5.99f)));
            Assert.IsTrue(board.IsInside(new Vector2(17.99f, 9.99f)));
        }

        [Test]
        public void IsInside_OnTheEdges_False()
        {
            var board = FakeBoard.Match(new Vector2(10f, 2f));

            Assert.IsFalse(board.IsInside(new Vector2(2f, 2f)));
            Assert.IsFalse(board.IsInside(new Vector2(18f, 2f)));
            Assert.IsFalse(board.IsInside(new Vector2(10f, -6f)));
            Assert.IsFalse(board.IsInside(new Vector2(10f, 10f)));
            Assert.IsFalse(board.IsInside(new Vector2(2f, -6f)));
        }

        [Test]
        public void IsInside_Outside_False()
        {
            var board = FakeBoard.Match(new Vector2(10f, 2f));

            Assert.IsFalse(board.IsInside(new Vector2(1.99f, 2f)));
            Assert.IsFalse(board.IsInside(new Vector2(18.01f, 2f)));
            Assert.IsFalse(board.IsInside(new Vector2(10f, -6.01f)));
            Assert.IsFalse(board.IsInside(new Vector2(10f, 10.01f)));
            Assert.IsFalse(board.IsInside(new Vector2(0f, 0f)));
        }

        [Test]
        public void IsInside_TwoBoards_APointIsInsideOnlyOne()
        {
            var own = FakeBoard.Match(new Vector2(0f, -9f));
            var other = FakeBoard.Match(new Vector2(0f, 9f));
            var point = new Vector2(3.3f, 5f);

            Assert.IsFalse(own.IsInside(point));
            Assert.IsTrue(other.IsInside(point));
        }

        [Test]
        public void WorldToBoardPosition_CentreOfEveryCell_ItsPosition()
        {
            var board = FakeBoard.Match(new Vector2(10f, 2f));

            foreach (var (position, cell) in board.Cells)
                Assert.AreEqual(position, board.WorldToBoardPosition(cell.WorldPosition), $"cell {position}");
        }

        [Test]
        public void WorldToBoardPosition_ShiftedBoardWithSmallCells_CentreOfEveryCell_ItsPosition()
        {
            var board = new FakeBoard(new Vector2Int(4, 3), 0.5f, new Vector2(2.5f, -3.5f));

            foreach (var (position, cell) in board.Cells)
                Assert.AreEqual(position, board.WorldToBoardPosition(cell.WorldPosition), $"cell {position}");
        }

        [Test]
        public void WorldToBoardPosition_CornersOfACell_TheCellOrTheNextOne()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            // Cell (3, 5) spans [-5, -4) x [-3, -2): the lower left corner is its own, the upper right one is the next cell.
            Assert.AreEqual(new Vector2Int(3, 5), board.WorldToBoardPosition(new Vector2(-5f, -3f)));
            Assert.AreEqual(new Vector2Int(3, 5), board.WorldToBoardPosition(new Vector2(-4.01f, -2.01f)));
            Assert.AreEqual(new Vector2Int(4, 6), board.WorldToBoardPosition(new Vector2(-4f, -2f)));
        }

        [Test]
        public void WorldToBoardPosition_OnTheLowerLeftEdge_FirstCells()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            Assert.AreEqual(new Vector2Int(0, 0), board.WorldToBoardPosition(new Vector2(-8f, -8f)));
            Assert.AreEqual(new Vector2Int(0, 7), board.WorldToBoardPosition(new Vector2(-8f, -0.5f)));
        }

        // The upper and right edges belong to no cell (a cell spans [start, start + size)): outside like any other
        // point past the board, so the position past the last cell (16, y) never comes out.
        [Test]
        public void WorldToBoardPosition_OnTheUpperRightEdge_Outside()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(8f, 8f)));
            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(8f, -4.5f)));
            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(-4.5f, 8f)));
        }

        [Test]
        public void WorldToBoardPosition_JustInsideTheUpperRightEdge_LastCells()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            Assert.AreEqual(new Vector2Int(15, 15), board.WorldToBoardPosition(new Vector2(7.99f, 7.99f)));
            Assert.AreEqual(new Vector2Int(15, 3), board.WorldToBoardPosition(new Vector2(7.99f, -4.5f)));
        }

        [Test]
        public void WorldToBoardPosition_Outside_MinusOne()
        {
            var board = FakeBoard.Match(new Vector2(10f, 2f));

            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(1.99f, 2f)));
            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(18.01f, 2f)));
            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(10f, -6.01f)));
            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(10f, 10.01f)));
            Assert.AreEqual(_outside, board.WorldToBoardPosition(new Vector2(0f, 0f)));
        }
    }
}
