using System.Collections.Generic;
using System.Linq;
using GamePlay.Boards;
using GamePlay.Cards;
using NUnit.Framework;
using UnityEngine;

namespace GamePlay.Tests
{
    // Reference of PatternShapes before board_cells_performance: the grids of the shapes, the cells they select around a
    // centre (row by row from the lower left corner of the grid) and their cut at the edges of the board.
    public class PatternShapesTests
    {
        [Test]
        public void Rhombus_Odd_DiamondOfTheSize()
        {
            AssertGrid(PatternShapes.Rhombus(1), "#");
            AssertGrid(PatternShapes.Rhombus(3),
                ".#.",
                "###",
                ".#.");
            AssertGrid(PatternShapes.Rhombus(5),
                "..#..",
                ".###.",
                "#####",
                ".###.",
                "..#..");
        }

        [Test]
        public void Rhombus_Even_DiamondWithAFlatTop()
        {
            AssertGrid(PatternShapes.Rhombus(2),
                "##",
                "##");
            AssertGrid(PatternShapes.Rhombus(4),
                ".##.",
                "####",
                "####",
                ".##.");
        }

        [Test]
        public void Line_HorizontalAndVertical_MiddleRowOrColumn()
        {
            AssertGrid(PatternShapes.Line(3, true),
                "...",
                "###",
                "...");
            AssertGrid(PatternShapes.Line(3, false),
                ".#.",
                ".#.",
                ".#.");
            AssertGrid(PatternShapes.Line(4, true),
                "....",
                "####",
                "....",
                "....");
        }

        [Test]
        public void Cross_ArmsOfTheSize()
        {
            AssertGrid(PatternShapes.Cross(1), "#");
            AssertGrid(PatternShapes.Cross(3),
                "..#..",
                "..#..",
                "#####",
                "..#..",
                "..#..");
        }

        [Test]
        public void Select_MinusOne_Empty()
        {
            var board = Board();

            Assert.IsEmpty(All(PatternShapes.Rhombus(3), board, new Vector2Int(-1, -1)));
        }

        [Test]
        public void All_OddShapeInTheMiddle_CentredRowByRow()
        {
            var board = Board();

            var cells = Positions(All(PatternShapes.Rhombus(3), board, new Vector2Int(5, 5)));

            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(5, 4),
                new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5),
                new Vector2Int(5, 6),
            }, cells);
        }

        [Test]
        public void All_EvenShape_ExtendsToTheLowerLeftOfTheCentre()
        {
            var board = Board();

            var square = Positions(All(PatternShapes.Rhombus(2), board, new Vector2Int(5, 5)));
            var line = Positions(All(PatternShapes.Line(4, true), board, new Vector2Int(5, 5)));

            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(4, 4), new Vector2Int(5, 4),
                new Vector2Int(4, 5), new Vector2Int(5, 5),
            }, square);
            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5),
            }, line);
        }

        [Test]
        public void All_Rhombus_LowerLeftCorner_CutByTheEdges()
        {
            var board = Board();

            var cells = Positions(All(PatternShapes.Rhombus(5), board, new Vector2Int(0, 0)));

            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1),
                new Vector2Int(0, 2),
            }, cells);
        }

        [Test]
        public void All_Rhombus_UpperRightCorner_CutByTheEdges()
        {
            var board = Board();

            var cells = Positions(All(PatternShapes.Rhombus(3), board, new Vector2Int(15, 15)));

            CollectionAssert.AreEqual(new[] { new Vector2Int(15, 14), new Vector2Int(14, 15), new Vector2Int(15, 15) }, cells);
        }

        [Test]
        public void All_Line_AtTheEdge_OnlyTheCellsOnTheBoard()
        {
            var board = Board();

            var horizontal = Positions(All(PatternShapes.Line(5, true), board, new Vector2Int(14, 3)));
            var vertical = Positions(All(PatternShapes.Line(5, false), board, new Vector2Int(3, 0)));

            CollectionAssert.AreEqual(new[] { new Vector2Int(12, 3), new Vector2Int(13, 3), new Vector2Int(14, 3), new Vector2Int(15, 3) },
                horizontal);
            CollectionAssert.AreEqual(new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(3, 2) }, vertical);
        }

        [Test]
        public void All_Cross_AtTheEdge_OnlyTheCellsOnTheBoard()
        {
            var board = Board();

            var cells = Positions(All(PatternShapes.Cross(3), board, new Vector2Int(0, 15)));

            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(0, 13),
                new Vector2Int(0, 14),
                new Vector2Int(0, 15), new Vector2Int(1, 15), new Vector2Int(2, 15),
            }, cells);
        }

        [Test]
        public void All_CentreOutsideTheBoard_TheCellsOfTheShapeOnTheBoard()
        {
            var board = Board();

            var cells = Positions(All(PatternShapes.Cross(3), board, new Vector2Int(-2, 4)));

            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 4) }, cells);
        }

        [Test]
        public void SelectTakenAndFree_SplitTheShapeByTheStatusOfTheCells()
        {
            var board = Board();
            board.Free(new Vector2Int(4, 5), new Vector2Int(5, 6));
            var shape = PatternShapes.Rhombus(3);

            var taken = Positions(SelectTaken(shape, board, new Vector2Int(5, 5)));
            var free = Positions(SelectFree(shape, board, new Vector2Int(5, 5)));

            CollectionAssert.AreEqual(new[] { new Vector2Int(5, 4), new Vector2Int(5, 5), new Vector2Int(6, 5) }, taken);
            CollectionAssert.AreEqual(new[] { new Vector2Int(4, 5), new Vector2Int(5, 6) }, free);
        }

        [Test]
        public void SelectTaken_AtTheCorner_OnlyTakenCellsOnTheBoard()
        {
            var board = Board();
            board.Free(new Vector2Int(1, 0));

            var taken = Positions(SelectTaken(PatternShapes.Rhombus(5), board, new Vector2Int(0, 0)));

            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(0, 0), new Vector2Int(2, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1),
                new Vector2Int(0, 2),
            }, taken);
        }

        [Test]
        public void Select_ReturnsTheCellsOfTheBoard()
        {
            var board = Board();

            var cells = All(PatternShapes.Cross(2), board, new Vector2Int(7, 7));

            foreach (var cell in cells)
                Assert.AreSame(board.Cells[cell.BoardPosition], cell);
        }

        private static List<IBoardCell> All(IPattenShape shape, IBoard board, Vector2Int center)
        {
            var result = new List<IBoardCell>();
            shape.All(board, center, result);
            return result;
        }

        private static List<IBoardCell> SelectTaken(IPattenShape shape, IBoard board, Vector2Int center)
        {
            var result = new List<IBoardCell>();
            shape.SelectTaken(board, center, result);
            return result;
        }

        private static List<IBoardCell> SelectFree(IPattenShape shape, IBoard board, Vector2Int center)
        {
            var result = new List<IBoardCell>();
            shape.SelectFree(board, center, result);
            return result;
        }

        private static FakeBoard Board()
        {
            return FakeBoard.Match(Vector2.zero);
        }

        private static List<Vector2Int> Positions(IReadOnlyList<IBoardCell> cells)
        {
            return cells.Select(cell => cell.BoardPosition).ToList();
        }

        // The rows from the top one down, '#' a cell of the shape. Positions[y][x] has y growing up.
        private static void AssertGrid(IPattenShape shape, params string[] rows)
        {
            var size = rows.Length;

            Assert.AreEqual(size, shape.Positions.Count, "rows");

            for (var y = 0; y < size; y++)
            {
                var row = rows[size - 1 - y];
                Assert.AreEqual(size, shape.Positions[y].Count, $"length of row {y}");

                for (var x = 0; x < size; x++)
                    Assert.AreEqual(row[x] == '#', shape.Positions[y][x], $"cell ({x}, {y})");
            }
        }
    }
}
