using Common.Extensions;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
using Shared;

namespace Benchmarks;

public class CellStateTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage)
        {
        }

        public override string Group => TestGroups.Game;
        public override string Title => "cell-state-transitions";
        public override string MetricName => "ms";

        protected override Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var options = Options.Create(new BoardOptions { Size = 8, Mines = 10 });
            var state = new ValueProperty<BoardState>(0).ForTest();
            var board = new Board(state, Guid.NewGuid(), options);

            // Manually set up a small board
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                    board.SetCell(new TakenCell(new Position(x, y), board));
            }

            handle.Progress.SetProgress(0.2f);

            // Test: Taken → Free transition
            var pos1 = new Position(3, 3);
            var takenCell = board.Cells[pos1];

            if (takenCell.Status != CellStatus.Taken)
                throw new Exception("Initial cell should be Taken");

            var freeCell = takenCell.ToFree();

            if (board.Cells[pos1].Status != CellStatus.Free)
                throw new Exception("Cell should be Free after ToFree()");

            handle.Progress.SetProgress(0.3f);

            // Test: Free → Taken transition
            var revertedCell = board.Cells[pos1].ToTaken();

            if (board.Cells[pos1].Status != CellStatus.Taken)
                throw new Exception("Cell should be Taken after ToTaken()");

            handle.Progress.SetProgress(0.4f);

            // Test: Mine placement
            var pos2 = new Position(5, 5);
            var mineCell = board.Cells[pos2].AsTaken();
            mineCell.SetMine();

            if (!mineCell.HasMine)
                throw new Exception("Cell should have mine after SetMine()");

            handle.Progress.SetProgress(0.5f);

            // Test: Flag operations
            var pos3 = new Position(2, 2);
            var flagCell = board.Cells[pos3].AsTaken();

            if (flagCell.IsFlagged)
                throw new Exception("Cell should not be flagged initially");

            flagCell.SetFlag();

            if (!flagCell.IsFlagged)
                throw new Exception("Cell should be flagged after SetFlag()");

            flagCell.RemoveFlag();

            if (flagCell.IsFlagged)
                throw new Exception("Cell should not be flagged after RemoveFlag()");

            handle.Progress.SetProgress(0.7f);

            // Test: Explode (doesn't change status, fires event)
            var pos4 = new Position(1, 1);
            var explodeCell = board.Cells[pos4].AsTaken();
            explodeCell.SetMine();
            explodeCell.Explode();

            // Cell is still Taken after explosion
            if (board.Cells[pos4].Status != CellStatus.Taken)
                throw new Exception("Cell should still be Taken after Explode()");

            handle.Progress.SetProgress(0.9f);

            // Test: ToFree on already Free cell returns self
            var pos5 = new Position(4, 4);
            board.Cells[pos5].ToFree();
            var sameFree = board.Cells[pos5].ToFree();

            if (board.Cells[pos5].Status != CellStatus.Free)
                throw new Exception("ToFree on Free cell should keep it Free");

            handle.Progress.Log("All cell state transition tests passed");
            handle.Progress.SetProgress(1f);

            return Task.CompletedTask;
        }
    }
}
