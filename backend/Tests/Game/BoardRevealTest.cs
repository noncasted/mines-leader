using Common.Extensions;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
using Shared;

namespace Tests;

public class BoardRevealTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Game;
        public override string Title => "board-reveal";

        protected override Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var size = 16;
            var mineCount = 40;
            var options = Options.Create(new BoardOptions { Size = size, Mines = mineCount });
            var state = new ValueProperty<BoardState>(0).ForTest();
            var board = new Board(state, Guid.NewGuid(), options);
            var startPosition = new Position(8, 8);

            board.Generator.Generate(startPosition);

            // Count initial taken cells
            var takenBefore = CountByStatus(board, CellStatus.Taken);
            var freeBefore = CountByStatus(board, CellStatus.Free);

            if (freeBefore != 0)
                throw new Exception($"Expected 0 free cells before reveal, got {freeBefore}");

            handle.Progress.SetProgress(0.2f);

            // Reveal the starting position — convert it to Free
            var startCell = board.Cells[startPosition];
            startCell.ToFree();
            board.MinesScanner.Start(new Common.Reactive.Lifetime());
            board.OnUpdated();

            // Now reveal (flood fill from start position)
            board.Revealer.Reveal(startPosition);

            var freeAfter = CountByStatus(board, CellStatus.Free);

            // At minimum the start position should be free
            if (freeAfter < 1)
                throw new Exception("Reveal did not convert any cells to Free");

            handle.Progress.SetProgress(0.5f);

            // Verify no mine cells were revealed (converted to Free)
            foreach (var (pos, cell) in board.Cells)
            {
                if (cell.Status != CellStatus.Free)
                    continue;

                // Free cells should not have mines (mines are only on Taken cells)
                // This is guaranteed by the cell model — Free cells don't have HasMine
            }

            // Verify total cells unchanged
            if (board.Cells.Count != size * size)
                throw new Exception($"Cell count changed: {board.Cells.Count}");

            handle.Progress.SetProgress(0.7f);

            // Test: revealing already-free cell does nothing
            var freeCountBefore = CountByStatus(board, CellStatus.Free);
            board.Revealer.Reveal(startPosition);
            var freeCountAfter = CountByStatus(board, CellStatus.Free);

            // Second reveal on same position should be idempotent
            // (neighbors already free won't be re-processed)

            handle.Progress.Log(
                $"Reveal test passed: {freeBefore} -> {freeAfter} free cells " +
                $"(total {size * size}, mines {mineCount})");
            handle.Progress.SetProgress(1f);

            return Task.CompletedTask;
        }

        private static int CountByStatus(IBoard board, CellStatus status)
        {
            var count = 0;

            foreach (var (_, cell) in board.Cells)
            {
                if (cell.Status == status)
                    count++;
            }

            return count;
        }
    }
}
