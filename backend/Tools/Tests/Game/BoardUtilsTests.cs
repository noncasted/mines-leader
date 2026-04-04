using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests board utility extension methods: NeighbourPositions, IterateNeighbours,
/// HasMinesAround, RandomPosition, GetClosedShape.
/// </summary>
public class BoardUtilsTests {
    [Fact]
    public void NeighbourPositions_Center_Returns8() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var neighbours = board.NeighbourPositions(new Position(2, 2));

        neighbours.Should().HaveCount(8);
        neighbours.Should().Contain(new Position(1, 1));
        neighbours.Should().Contain(new Position(2, 1));
        neighbours.Should().Contain(new Position(3, 1));
        neighbours.Should().Contain(new Position(1, 2));
        neighbours.Should().Contain(new Position(3, 2));
        neighbours.Should().Contain(new Position(1, 3));
        neighbours.Should().Contain(new Position(2, 3));
        neighbours.Should().Contain(new Position(3, 3));
    }

    [Fact]
    public void NeighbourPositions_TopLeftCorner_Returns3() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var neighbours = board.NeighbourPositions(new Position(0, 0));

        neighbours.Should().HaveCount(3);
        neighbours.Should().Contain(new Position(1, 0));
        neighbours.Should().Contain(new Position(0, 1));
        neighbours.Should().Contain(new Position(1, 1));
    }

    [Fact]
    public void NeighbourPositions_TopRightCorner_Returns3() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var neighbours = board.NeighbourPositions(new Position(4, 0));

        neighbours.Should().HaveCount(3);
        neighbours.Should().Contain(new Position(3, 0));
        neighbours.Should().Contain(new Position(3, 1));
        neighbours.Should().Contain(new Position(4, 1));
    }

    [Fact]
    public void NeighbourPositions_BottomLeftCorner_Returns3() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var neighbours = board.NeighbourPositions(new Position(0, 4));

        neighbours.Should().HaveCount(3);
        neighbours.Should().Contain(new Position(0, 3));
        neighbours.Should().Contain(new Position(1, 3));
        neighbours.Should().Contain(new Position(1, 4));
    }

    [Fact]
    public void NeighbourPositions_BottomRightCorner_Returns3() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var neighbours = board.NeighbourPositions(new Position(4, 4));

        neighbours.Should().HaveCount(3);
        neighbours.Should().Contain(new Position(3, 3));
        neighbours.Should().Contain(new Position(4, 3));
        neighbours.Should().Contain(new Position(3, 4));
    }

    [Fact]
    public void NeighbourPositions_Edge_Returns5() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var neighbours = board.NeighbourPositions(new Position(2, 0)); // top edge

        neighbours.Should().HaveCount(5);
        neighbours.Should().Contain(new Position(1, 0));
        neighbours.Should().Contain(new Position(3, 0));
        neighbours.Should().Contain(new Position(1, 1));
        neighbours.Should().Contain(new Position(2, 1));
        neighbours.Should().Contain(new Position(3, 1));
    }

    [Fact]
    public void IterateNeighbours_Center_VisitsAll8() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var visited = new List<Position>();

        board.IterateNeighbours(new Position(2, 2), pos => visited.Add(pos));

        visited.Should().HaveCount(8);
    }

    [Fact]
    public void IterateNeighbours_Corner_VisitsOnly3() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
        var visited = new List<Position>();

        board.IterateNeighbours(new Position(0, 0), pos => visited.Add(pos));

        visited.Should().HaveCount(3);
    }

    [Fact]
    public void HasMinesAround_WithAdjacentMine_ReturnsTrue() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t m t t
            t t t t t
            t t t t t
            """);

        board.HasMinesAround(new Position(1, 1)).Should().BeTrue();
        board.HasMinesAround(new Position(2, 1)).Should().BeTrue();
        board.HasMinesAround(new Position(3, 3)).Should().BeTrue();
    }

    [Fact]
    public void HasMinesAround_WithNoAdjacentMine_ReturnsFalse() {
        var (board, _) = BoardParser.Parse("""
            m t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);

        // (3,3) is far from (0,0) — no mines around
        board.HasMinesAround(new Position(3, 3)).Should().BeFalse();
        board.HasMinesAround(new Position(4, 4)).Should().BeFalse();
    }

    [Fact]
    public void HasMinesAround_OnMinePosition_DetectsAdjacentMines() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t m t t
            t t m t t
            t t t t t
            """);

        // (2,2) has a mine at (2,3) adjacent to it
        board.HasMinesAround(new Position(2, 2)).Should().BeTrue();
    }

    [Fact]
    public void HasMinesAround_EmptyBoard_ReturnsFalse() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);

        board.HasMinesAround(new Position(2, 2)).Should().BeFalse();
    }

    [Fact]
    public void HasMinesAround_IgnoresFreeCells() {
        // HasMinesAround only checks Taken cells for mines
        // Mine at (2,2) but cell is Free — FreeCell has no HasMine
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t _ t t
            t t t t t
            t t t t t
            """);

        // (1,1) adjacent to (2,2) which is Free — should return false
        board.HasMinesAround(new Position(1, 1)).Should().BeFalse();
    }

    [Fact]
    public void RandomPosition_WithinBounds() {
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

        for (var i = 0; i < 100; i++) {
            var pos = board.RandomPosition();
            pos.x.Should().BeGreaterThanOrEqualTo(0);
            pos.x.Should().BeLessThan(8);
            pos.y.Should().BeGreaterThanOrEqualTo(0);
            pos.y.Should().BeLessThan(8);
        }
    }

    [Fact]
    public void RandomPosition_ProducesVariety() {
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
        var positions = new HashSet<Position>();

        for (var i = 0; i < 200; i++)
            positions.Add(board.RandomPosition());

        positions.Count.Should().BeGreaterThan(10, "200 iterations on 64 cells should produce many distinct positions");
    }

    [Fact]
    public void GetClosedShape_InvalidPosition_ReturnsEmpty() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);

        var result = board.GetClosedShape(new Position(-1, -1));

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetClosedShape_AllTaken_NoFreeBorder_ReturnsEmpty() {
        // GetClosedShape finds Taken cells that are adjacent to Free cells
        // If no Free cells exist, no Taken cells qualify
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);

        var result = board.GetClosedShape(new Position(2, 2));

        result.Should().BeEmpty("no Free cells to form a border");
    }

    [Fact]
    public void GetClosedShape_TakenCellAdjacentToFree_IncludesIt() {
        // Create a board with some free cells to form a border
        var (board, _) = BoardParser.Parse("""
            _ _ t t t
            _ t t t t
            t t t t t
            t t t t t
            t t t t t
            """);

        // (1,1) is Taken and adjacent to Free cells at (0,0), (1,0), (0,1)
        var result = board.GetClosedShape(new Position(1, 1));

        result.Should().NotBeEmpty();
        result.Select(c => c.Position).Should().Contain(new Position(1, 1));
    }

    [Fact]
    public void GetClosedShape_ConnectedRegion_IncludesAllConnected() {
        // Free cells surround a group of Taken cells
        var (board, _) = BoardParser.Parse("""
            _ _ _ _ _
            _ t t t _
            _ t t t _
            _ _ _ _ _
            t t t t t
            """);

        // Inner Taken cells at (1,1), (2,1), (3,1), (1,2), (2,2), (3,2)
        var result = board.GetClosedShape(new Position(2, 2));

        // All inner Taken cells should be part of the closed shape
        var positions = result.Select(c => c.Position).ToHashSet();
        positions.Should().Contain(new Position(1, 1));
        positions.Should().Contain(new Position(2, 1));
        positions.Should().Contain(new Position(3, 1));
        positions.Should().Contain(new Position(1, 2));
        positions.Should().Contain(new Position(2, 2));
        positions.Should().Contain(new Position(3, 2));
    }

    [Fact]
    public void GetClosedShape_StartOnFreeCell_ReturnsEmpty() {
        var (board, _) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t _ t t
            t t t t t
            t t t t t
            """);

        // Starting from Free cell — GetClosedShape returns true early for Free,
        // doesn't add to selected
        var result = board.GetClosedShape(new Position(2, 2));

        result.Should().BeEmpty("starting on a Free cell means nothing to select");
    }

    [Fact]
    public void Board_Size_MatchesConfiguration() {
        var (board, _) = BoardParser.Parse("""
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            """);

        board.Size.Should().Be(new Position(7, 7));
        board.Cells.Count.Should().Be(49);
    }

    [Fact]
    public void Board_OwnerId_IsSet() {
        var ownerId = Guid.NewGuid();
        var board = new TestBoardBuilder(3)
            .WithOwner(ownerId)
            .Build();

        board.OwnerId.Should().Be(ownerId);
    }
}
