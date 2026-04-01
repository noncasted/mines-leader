using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests BoardGenerator.Generate() — populates the board with cells and mines,
/// ensuring the start position and its neighbours are always mine-free.
/// </summary>
public class BoardGenerationTests {
    private static IBoard CreateAndGenerate(int size, int mines, Position start) {
        var options = Options.Create(new BoardOptions { Size = size, Mines = mines });
        var state = new ValueProperty<BoardState>(0).ForTest();
        var board = new Board(state, Guid.NewGuid(), options);
        board.Generator.Generate(start);
        return board;
    }

    [Fact]
    public void Generate_CreatesCorrectBoardSize() {
        var board = CreateAndGenerate(size: 8, mines: 10, start: new Position(4, 4));

        board.Cells.Count.Should().Be(64);
        board.Cells.Values.Should().AllSatisfy(cell =>
            cell.Status.Should().Be(CellStatus.Taken));
    }

    [Fact]
    public void Generate_PlacesExactMineCount() {
        var board = CreateAndGenerate(size: 10, mines: 15, start: new Position(5, 5));

        var mineCount = board.Cells.Values.Count(c => c.ToTaken().HasMine);
        mineCount.Should().Be(15);
    }

    [Fact]
    public void Generate_StartPositionIsMinesFree() {
        var start = new Position(4, 4);
        var board = CreateAndGenerate(size: 8, mines: 20, start: start);

        board.Cells[start].ToTaken().HasMine.Should().BeFalse();
    }

    [Fact]
    public void Generate_StartNeighboursAreMinesFree() {
        var start = new Position(4, 4);
        var board = CreateAndGenerate(size: 8, mines: 20, start: start);

        var neighbours = board.NeighbourPositions(start);
        neighbours.Should().HaveCount(8);

        foreach (var neighbour in neighbours) {
            board.Cells[neighbour].ToTaken().HasMine.Should().BeFalse(
                $"neighbour {neighbour} of start should not have a mine");
        }
    }

    [Fact]
    public void Generate_CornerStart_NeighboursAreMinesFree() {
        var start = new Position(0, 0);
        var board = CreateAndGenerate(size: 8, mines: 20, start: start);

        board.Cells[start].ToTaken().HasMine.Should().BeFalse();

        var neighbours = board.NeighbourPositions(start);
        neighbours.Should().HaveCount(3);

        foreach (var neighbour in neighbours) {
            board.Cells[neighbour].ToTaken().HasMine.Should().BeFalse(
                $"neighbour {neighbour} of corner start should not have a mine");
        }
    }

    [Fact]
    public void Generate_MinesDistributedRandomly() {
        var allMinePositions = new HashSet<Position>();

        for (var i = 0; i < 50; i++) {
            var board = CreateAndGenerate(size: 10, mines: 20, start: new Position(5, 5));
            foreach (var cell in board.Cells.Values) {
                if (cell.ToTaken().HasMine)
                    allMinePositions.Add(cell.Position);
            }
        }

        allMinePositions.Count.Should().BeGreaterThanOrEqualTo(4,
            "mines should appear in different positions across multiple runs");
    }

    [Fact]
    public void Generate_MaxMines_FillsAllNonSafePositions() {
        var start = new Position(1, 1);
        var size = 4;
        var safeZone = new HashSet<Position> { start };
        var options = Options.Create(new BoardOptions { Size = size, Mines = 0 });
        var state = new ValueProperty<BoardState>(0).ForTest();
        var board = new Board(state, Guid.NewGuid(), options);

        // Calculate safe zone size (start + neighbours)
        foreach (var dir in BoardPositionsExtensions.Directions) {
            var neighbour = start + dir;
            if (neighbour.x >= 0 && neighbour.x < size && neighbour.y >= 0 && neighbour.y < size)
                safeZone.Add(neighbour);
        }

        var totalCells = size * size;
        var maxMines = totalCells - safeZone.Count;

        // Create board with max mines
        var boardWithMaxMines = CreateAndGenerate(size: size, mines: maxMines, start: start);

        var mineCount = boardWithMaxMines.Cells.Values.Count(c => c.ToTaken().HasMine);
        mineCount.Should().Be(maxMines);

        // All safe zone cells should be mine-free
        foreach (var safePos in safeZone) {
            boardWithMaxMines.Cells[safePos].ToTaken().HasMine.Should().BeFalse(
                $"safe zone cell {safePos} should not have a mine");
        }

        // All non-safe cells should have mines
        foreach (var cell in boardWithMaxMines.Cells.Values) {
            if (safeZone.Contains(cell.Position))
                continue;
            cell.ToTaken().HasMine.Should().BeTrue(
                $"non-safe cell {cell.Position} should have a mine when all slots are filled");
        }
    }
}
