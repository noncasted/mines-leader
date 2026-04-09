using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

public class PatternShapesTests
{
    private static int CountTrue(IPattenShape shape)
    {
        var count = 0;

        for (var y = 0; y < shape.Positions.Count; y++)
            for (var x = 0; x < shape.Positions[y].Count; x++)
                if (shape.Positions[y][x])
                    count++;
        return count;
    }

    [Fact]
    public void Rhombus3_CrossShape_FiveCells()
    {
        var shape = PatternShapes.Rhombus(3);

        shape.Positions.Count.Should().Be(3);
        shape.Positions[0].Count.Should().Be(3);
        CountTrue(shape).Should().Be(5);

        // Cross pattern: center column and center row
        shape.Positions[0][1].Should().BeTrue(); // top center
        shape.Positions[1][0].Should().BeTrue(); // middle left
        shape.Positions[1][1].Should().BeTrue(); // center
        shape.Positions[1][2].Should().BeTrue(); // middle right
        shape.Positions[2][1].Should().BeTrue(); // bottom center

        // Corners should be false
        shape.Positions[0][0].Should().BeFalse();
        shape.Positions[0][2].Should().BeFalse();
        shape.Positions[2][0].Should().BeFalse();
        shape.Positions[2][2].Should().BeFalse();
    }

    [Fact]
    public void Rhombus4_EvenDiamond()
    {
        var shape = PatternShapes.Rhombus(4);

        shape.Positions.Count.Should().Be(4);
        shape.Positions[0].Count.Should().Be(4);

        // Rhombus(4): even → expand to 6, build 6x6 diamond, trim to 4x4
        // Result: 2+4+4+2 = 12 cells
        var trueCount = CountTrue(shape);
        trueCount.Should().Be(12);

        // Verify symmetry (same approach as Rhombus_Symmetric)
        var size = shape.Positions.Count;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!shape.Positions[y][x])
                    continue;

                shape.Positions[size - 1 - y][x]
                     .Should()
                     .BeTrue($"vertical symmetry failed at ({y},{x})");

                shape.Positions[y][size - 1 - x]
                     .Should()
                     .BeTrue($"horizontal symmetry failed at ({y},{x})");
            }
        }
    }

    [Fact]
    public void Rhombus5_LargerDiamond_ThirteenCells()
    {
        var shape = PatternShapes.Rhombus(5);

        shape.Positions.Count.Should().Be(5);
        shape.Positions[0].Count.Should().Be(5);
        CountTrue(shape).Should().Be(13);
    }

    [Fact]
    public void Rhombus_Symmetric()
    {
        var shape = PatternShapes.Rhombus(7);
        var size = shape.Positions.Count;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!shape.Positions[y][x])
                    continue;

                shape.Positions[size - 1 - y][x]
                     .Should()
                     .BeTrue($"vertical symmetry failed at ({y},{x}) vs ({size - 1 - y},{x})");

                shape.Positions[y][size - 1 - x]
                     .Should()
                     .BeTrue($"horizontal symmetry failed at ({y},{x}) vs ({y},{size - 1 - x})");
            }
        }
    }

    [Fact]
    public void SelectTaken_FiltersOnlyTakenCells()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t
                                           t t t t t t t
                                           t t t _ t t t
                                           t t t t t t t
                                           t t t _ t t t
                                           t t t t t t t
                                           t t t t t t t
                                           """);

        var shape = PatternShapes.Rhombus(3);
        var result = shape.SelectTaken(board, new Position(3, 3));

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(cell => cell.Status.Should().Be(CellStatus.Taken));
        result.Should().NotContain(cell => cell.Position == new Position(3, 2));
        result.Should().NotContain(cell => cell.Position == new Position(3, 4));
    }

    [Fact]
    public void SelectFree_FiltersOnlyFreeCells()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t
                                           t t t t t t t
                                           t t t _ t t t
                                           t t t t t t t
                                           t t t _ t t t
                                           t t t t t t t
                                           t t t t t t t
                                           """);

        var shape = PatternShapes.Rhombus(3);
        var result = shape.SelectFree(board, new Position(3, 3));

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(cell => cell.Status.Should().Be(CellStatus.Free));
    }

    [Fact]
    public void Select_ClipsAtTopLeftEdge()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           """);
        var shape = PatternShapes.Rhombus(5);

        var result = shape.SelectAll(board, new Position(0, 0));

        result.Count.Should().BeLessThan(13);

        result.Should()
              .AllSatisfy(cell => {
                  cell.Position.x.Should().BeGreaterThanOrEqualTo(0);
                  cell.Position.y.Should().BeGreaterThanOrEqualTo(0);
              });
    }

    [Fact]
    public void Select_ClipsAtBottomRightEdge()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           """);
        var shape = PatternShapes.Rhombus(5);

        var result = shape.SelectAll(board, new Position(7, 7));

        result.Count.Should().BeLessThan(13);
    }

    [Fact]
    public void Select_CenterOfBoard_NoClipping()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           """);
        var shape = PatternShapes.Rhombus(5);

        var result = shape.SelectAll(board, new Position(5, 5));

        result.Count.Should().Be(13);
    }

    [Fact]
    public void Select_InvalidCenter_ReturnsEmpty()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           """);
        var shape = PatternShapes.Rhombus(5);

        var result = shape.Select(board, new Position(-1, -1), _ => true);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Line_HorizontalShape()
    {
        var shape = PatternShapes.Line(5, horizontal: true);

        shape.Positions.Count.Should().Be(5);
        shape.Positions[0].Count.Should().Be(5);
        CountTrue(shape).Should().Be(5);

        // Only center row (index 2) should be all true
        for (var x = 0; x < 5; x++)
        {
            shape.Positions[2][x].Should().BeTrue($"center row at column {x}");
        }

        // Other rows should be all false
        for (var y = 0; y < 5; y++)
        {
            if (y == 2)
                continue;

            for (var x = 0; x < 5; x++)
            {
                shape.Positions[y][x].Should().BeFalse($"row {y} column {x} should be false");
            }
        }
    }

    [Fact]
    public void Line_VerticalShape()
    {
        var shape = PatternShapes.Line(5, horizontal: false);

        shape.Positions.Count.Should().Be(5);
        shape.Positions[0].Count.Should().Be(5);
        CountTrue(shape).Should().Be(5);

        // Only center column (index 2) should be all true
        for (var y = 0; y < 5; y++)
        {
            shape.Positions[y][2].Should().BeTrue($"center column at row {y}");
        }

        // Other columns should be all false
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                if (x == 2)
                    continue;
                shape.Positions[y][x].Should().BeFalse($"row {y} column {x} should be false");
            }
        }
    }
}