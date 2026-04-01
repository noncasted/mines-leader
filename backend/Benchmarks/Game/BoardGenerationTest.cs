using Common.Extensions;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
using Shared;

namespace Benchmarks;

public class BoardGenerationTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage)
        {
        }

        public override string Group => TestGroups.Game;
        public override string Title => "board-generation";
        public override string MetricName => "ms";

        protected override Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var size = 16;
            var mineCount = 40;
            var options = Options.Create(new BoardOptions { Size = size, Mines = mineCount });
            var state = new ValueProperty<BoardState>(0).ForTest();
            var ownerId = Guid.NewGuid();

            var board = new Board(state, ownerId, options);
            var startPosition = new Position(8, 8);

            // Generate board
            board.Generator.Generate(startPosition);
            handle.Progress.SetProgress(0.3f);

            // Verify board size
            var expectedCells = size * size;

            if (board.Cells.Count != expectedCells)
                throw new Exception($"Board has {board.Cells.Count} cells, expected {expectedCells}");

            // Count mines
            var actualMines = 0;

            foreach (var (_, cell) in board.Cells)
            {
                if (cell.Status == CellStatus.Taken && cell.AsTaken().HasMine)
                    actualMines++;
            }

            if (actualMines != mineCount)
                throw new Exception($"Board has {actualMines} mines, expected {mineCount}");

            handle.Progress.SetProgress(0.6f);

            // Verify starting position and neighbors are mine-free
            var safePositions = board.NeighbourPositions(startPosition);
            safePositions.Add(startPosition);

            foreach (var safePos in safePositions)
            {
                if (!board.Cells.TryGetValue(safePos, out var cell))
                    continue;

                if (cell.Status == CellStatus.Taken && cell.AsTaken().HasMine)
                    throw new Exception($"Mine found at safe position {safePos}");
            }

            handle.Progress.SetProgress(0.8f);

            // Verify all positions are within bounds
            foreach (var (pos, _) in board.Cells)
            {
                if (pos.x < 0 || pos.x >= size || pos.y < 0 || pos.y >= size)
                    throw new Exception($"Cell at {pos} is out of bounds");
            }

            handle.Progress.Log($"Board verified: {expectedCells} cells, {actualMines} mines, start position safe");
            handle.Progress.SetProgress(1f);

            return Task.CompletedTask;
        }
    }
}
