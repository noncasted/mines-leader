using GamePlay.Boards;
using NUnit.Framework;
using UnityEngine;

namespace GamePlay.Tests
{
    // Reference of BoardExtensions before board_cells_performance: the world bounds come from the cells, IsInside is
    // strict, WorldToBoardPosition counts cells from the lower left corner of the bounds.
    public class BoardGeometryTests
    {
        private static readonly Vector2Int _outside = new(-1, -1);

        [Test]
        public void WorldBounds_MatchBoard_HalfACellAroundTheCentresOfTheCells()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            var (min, max) = board.GetBoardWorldBounds();

            Assert.AreEqual(new Vector2(-8f, -8f), min);
            Assert.AreEqual(new Vector2(8f, 8f), max);
        }

        [Test]
        public void WorldBounds_ShiftedBoardWithSmallCells_FollowTheCells()
        {
            var board = new FakeBoard(new Vector2Int(4, 3), 0.5f, new Vector2(2.5f, -3.5f));

            var (min, max) = board.GetBoardWorldBounds();

            Assert.AreEqual(new Vector2(2.25f, -3.75f), min);
            Assert.AreEqual(new Vector2(4.25f, -2.25f), max);
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

        // Current behaviour: the upper and right edges count as inside and give the position past the last cell, a
        // position that has no cell (callers look it up in Cells and find nothing).
        [Test]
        public void WorldToBoardPosition_OnTheUpperRightEdge_PositionPastTheLastCell()
        {
            var board = FakeBoard.Match(new Vector2(0f, 0f));

            Assert.AreEqual(new Vector2Int(16, 16), board.WorldToBoardPosition(new Vector2(8f, 8f)));
            Assert.AreEqual(new Vector2Int(16, 3), board.WorldToBoardPosition(new Vector2(8f, -4.5f)));
            Assert.IsFalse(board.Cells.ContainsKey(new Vector2Int(16, 3)));
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
