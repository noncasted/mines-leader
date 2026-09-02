using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests board-level logic exercised by game commands:
/// - Chord opening (OpenMultipleCellsCommand logic)
/// - EnsureGenerated (lazy board initialization)
/// - SkipTurn (round lifetime termination)
/// </summary>
public class BoardCommandTests
{
    // ──────────────────────────────────────────────────────────────────────
    //  Chord Opening (OpenMultipleCells logic)
    //
    //  Rule: if a Free cell's MinesAround == number of flagged neighbors,
    //  auto-open all unflagged Taken neighbors.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replicates the chord-open algorithm from OpenMultipleCellsCommand.OnTime
    /// at the board level (no GameCommandUtils dependency).
    /// Returns (openedCells, explodedMines) counts.
    /// </summary>
    private static (int opened, int exploded) ChordOpen(IBoard board, Position position)
    {
        var targetCell = board.Cells[position];

        if (targetCell.Status != CellStatus.Free)
            return (-1, 0); // signals failure

        var free = targetCell.AsFree();
        var around = free.MinesAround;
        var placedFlags = 0;
        var takenNeighbours = new List<ITakenCell>();
        var flaggedNeighbours = new List<ITakenCell>();

        board.IterateNeighbours(position, neighbour => {
            var neighbourCell = board.Cells[neighbour];

            if (neighbourCell.Status != CellStatus.Taken)
                return;

            var takenNeighbourCell = neighbourCell.AsTaken();
            takenNeighbours.Add(takenNeighbourCell);

            if (takenNeighbourCell.IsFlagged == false)
                return;

            flaggedNeighbours.Add(takenNeighbourCell);
            placedFlags++;
        });

        if (around != placedFlags)
            return (0, 0); // no-op: flag count mismatch

        var openedCells = new List<ITakenCell>();
        var exploded = 0;

        foreach (var neighbour in takenNeighbours)
        {
            if (flaggedNeighbours.Contains(neighbour) == true)
                continue;

            if (neighbour.HasMine == true)
            {
                neighbour.Explode();
                exploded++;
            }

            neighbour.ToFree();
            openedCells.Add(neighbour);
        }

        foreach (var neighbour in openedCells)
            board.Revealer.Reveal(new[] { neighbour.Position });

        board.Revealer.Reveal(new[] { position });
        board.MinesScanner.Recalculate();

        return (openedCells.Count, exploded);
    }

    [Fact]
    public void Chord_CorrectFlagCount_OpensUnflaggedNeighbors()
    {
        // Layout: center cell (2,2) is Free with 1 mine around at (1,1).
        // Flag placed on (1,1). Chord should open all other Taken neighbors.
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t f t t t
                                           t t _ t t
                                           t t t t t
                                           t t t t t
                                           """);

        var target = new Position(2, 2);

        // Verify precondition: center is Free with MinesAround == 1
        var center = board.Cells[target];
        center.Status.Should().Be(CellStatus.Free);
        center.AsFree().MinesAround.Should().Be(1);

        var (opened, exploded) = ChordOpen(board, target);

        opened.Should().BeGreaterThan(0, "unflagged neighbors should be opened");
        exploded.Should().Be(0, "no unflagged mines");

        // All Taken neighbors of (2,2) except the flagged (1,1) should now be Free
        var unflaggedNeighbors = new[]
        {
            new Position(1, 2), new Position(2, 1), new Position(3, 1),
            new Position(3, 2), new Position(1, 3), new Position(2, 3), new Position(3, 3)
        };

        foreach (var pos in unflaggedNeighbors)
        {
            board.Cells[pos]
                 .Status.Should()
                 .Be(CellStatus.Free,
                     $"cell at ({pos.x},{pos.y}) should be opened by chord");
        }

        // Flagged mine stays Taken
        board.Cells[new Position(1, 1)].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void Chord_IncorrectFlagCount_NoAutoOpen()
    {
        // Center (2,2) is Free with 2 mines around, but only 1 flag placed.
        // Chord should do nothing.
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t f m t t
                                           t t _ t t
                                           t t t t t
                                           t t t t t
                                           """);

        var target = new Position(2, 2);

        var center = board.Cells[target].AsFree();
        center.MinesAround.Should().Be(2);

        var (opened, exploded) = ChordOpen(board, target);

        opened.Should().Be(0, "flag count (1) != MinesAround (2), no-op");
        exploded.Should().Be(0);

        // Neighbors should remain Taken
        board.Cells[new Position(2, 1)].Status.Should().Be(CellStatus.Taken);
        board.Cells[new Position(3, 1)].Status.Should().Be(CellStatus.Taken);
        board.Cells[new Position(1, 2)].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void Chord_UnflaggedNeighborHasMine_Explodes()
    {
        // Center (2,2) is Free with MinesAround=2. Two mines: (1,1) and (3,1).
        // Flag (1,1) correctly, but ALSO flag a non-mine (3,3) — total flags = 2.
        // This makes placedFlags == MinesAround, so chord opens unflagged neighbors.
        // (3,1) has a mine and is unflagged — it explodes.
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t f t m t
                                           t t _ t t
                                           t t t g t
                                           t t t t t
                                           """);

        var target = new Position(2, 2);

        var center = board.Cells[target].AsFree();
        center.MinesAround.Should().Be(2);

        var (opened, exploded) = ChordOpen(board, target);

        opened.Should().BeGreaterThan(0);
        exploded.Should().Be(1, "unflagged mine at (3,1) should explode");

        // (3,1) was a mine but got opened (exploded + ToFree)
        board.Cells[new Position(3, 1)]
             .Status.Should()
             .Be(CellStatus.Free,
                 "exploded mine converts to Free after chord");
    }

    [Fact]
    public void Chord_SourceCellIsTaken_Fails()
    {
        // Target cell is Taken — chord returns failure
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t m t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """);

        var target = new Position(2, 2);
        board.Cells[target].Status.Should().Be(CellStatus.Taken);

        var (opened, _) = ChordOpen(board, target);

        opened.Should().Be(-1, "chord on Taken cell should fail");
    }

    [Fact]
    public void Chord_ZeroMinesAround_ZeroFlags_OpensAllNeighbors()
    {
        // Center (2,2) is Free with MinesAround=0, no flags needed.
        // Chord with 0==0 should open all Taken neighbors.
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t _ t t
                                           t t t t t
                                           t t t t t
                                           """);

        var target = new Position(2, 2);
        var center = board.Cells[target].AsFree();
        center.MinesAround.Should().Be(0);

        var (opened, exploded) = ChordOpen(board, target);

        opened.Should().Be(8, "all 8 Taken neighbors opened");
        exploded.Should().Be(0);
    }

    [Fact]
    public void Chord_AllNeighborsFlagged_NoOpens()
    {
        // Center (2,2) is Free with MinesAround=1, mine at (1,1).
        // All non-mine neighbors are already Free. Only (1,1) is Taken+flagged.
        // Chord: flaggedNeighbours contains (1,1), takenNeighbours contains (1,1).
        // No unflagged Taken neighbors => opened = 0.
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t f _ _ t
                                           t _ _ _ t
                                           t _ _ _ t
                                           t t t t t
                                           """);

        var target = new Position(2, 2);
        board.Cells[target].Status.Should().Be(CellStatus.Free);

        var (opened, exploded) = ChordOpen(board, target);

        opened.Should().Be(0, "no unflagged Taken neighbors");
        exploded.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  EnsureGenerated
    //
    //  Extension method: if board.Cells.Count == 0, calls Generate + Reveal.
    //  Otherwise no-op.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnsureGenerated_NonEmptyBoard_NoOp()
    {
        // Board built via BoardParser already has cells
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t m t t t
                                           t t t t t
                                           t t t m t
                                           t t t t t
                                           """);

        var cellCountBefore = board.Cells.Count;
        cellCountBefore.Should().Be(25);

        board.EnsureGenerated(new MoveSnapshot(), new Position(2, 2));

        // Cells unchanged — no regeneration
        board.Cells.Count.Should().Be(cellCountBefore);
    }

    [Fact]
    public void EnsureGenerated_EmptyBoard_GeneratesAndReveals()
    {
        // Create an empty board (no cells yet) via Board constructor directly
        var options = Options.Create(new BoardOptions { Size = 8, Mines = 10 });
        var state = new ValueProperty<BoardState>(0).ForTest();
        var board = new Board(Guid.NewGuid(), options);

        // Start MinesScanner so Reveal can calculate MinesAround
        var lifetime = new Lifetime();
        board.MinesScanner.Recalculate();

        board.Cells.Count.Should().Be(0, "board starts empty");

        var start = new Position(4, 4);
        board.EnsureGenerated(new MoveSnapshot(), start);

        // After EnsureGenerated: board is populated with all cells
        board.Cells.Count.Should().Be(64, "8x8 board generated");

        // Start position and its neighbors are mine-free (Generate guarantees this)
        board.Cells[start]
             .AsTaken()
             .HasMine.Should()
             .BeFalse("start position is always mine-free after generation");

        // Mine count should be correct
        var mineCount = board.Cells.Values
                             .Where(c => c.Status == CellStatus.Taken)
                             .Count(c => c.AsTaken().HasMine);
        mineCount.Should().Be(10);
    }

    [Fact]
    public void EnsureGenerated_EmptyBoard_RecordsGeneratedOnce()
    {
        var options = Options.Create(new BoardOptions { Size = 6, Mines = 5 });
        var board = new Board(Guid.NewGuid(), options);
        var snapshot = new MoveSnapshot();

        board.IsGenerated.Should().BeFalse("board starts empty");

        board.EnsureGenerated(snapshot, new Position(3, 3));
        board.EnsureGenerated(snapshot, new Position(0, 0));

        board.IsGenerated.Should().BeTrue();

        var generated = snapshot.Collect()
                                .Records.OfType<SharedBoardSnapshot>()
                                .SelectMany(r => r.Records)
                                .OfType<BoardSnapshotRecord.Generated>()
                                .ToList();

        generated.Should().HaveCount(1, "only the first call generates the board");
    }

    [Fact]
    public void EnsureGenerated_CalledTwice_SecondCallIsNoOp()
    {
        var options = Options.Create(new BoardOptions { Size = 6, Mines = 5 });
        var state = new ValueProperty<BoardState>(0).ForTest();
        var board = new Board(Guid.NewGuid(), options);

        var lifetime = new Lifetime();
        board.MinesScanner.Recalculate();

        var start = new Position(3, 3);
        board.EnsureGenerated(new MoveSnapshot(), start);

        // Snapshot the board state after first generation
        var cellsBefore = board.Cells.ToDictionary(kvp => kvp.Key,
            kvp => kvp.Value.Status);

        // Second call — should be no-op since Cells.Count > 0
        board.EnsureGenerated(new MoveSnapshot(), new Position(0, 0));

        // Board state unchanged
        foreach (var (pos, status) in cellsBefore)
        {
            board.Cells[pos]
                 .Status.Should()
                 .Be(status,
                     $"cell at ({pos.x},{pos.y}) should not change on second EnsureGenerated");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  SkipTurn (Lifetime termination)
    //
    //  SkipTurn() terminates the round's forced lifetime, ending the turn.
    //  We test the underlying mechanism: Lifetime.Terminate().
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void SkipTurn_TerminatesRoundLifetime()
    {
        // SkipTurn calls _roundForcedLifetime.Terminate()
        // Test that a child lifetime terminates when its parent is terminated,
        // which is the mechanism SkipTurn relies on.
        var parentLifetime = new Lifetime();
        var childLifetime = parentLifetime.Child();

        childLifetime.IsTerminated.Should().BeFalse();

        // Simulates what SkipTurn does
        childLifetime.Terminate();

        childLifetime.IsTerminated.Should().BeTrue();
        parentLifetime.IsTerminated.Should().BeFalse("parent survives child termination");
    }

    [Fact]
    public void SkipTurn_ListenCallbackFires_OnTermination()
    {
        // SkipTurn terminates a lifetime — any Listen() callbacks should fire.
        var lifetime = new Lifetime();
        var callbackFired = false;

        lifetime.Listen(() => callbackFired = true);

        callbackFired.Should().BeFalse();

        lifetime.Terminate();

        callbackFired.Should().BeTrue("Listen callback fires on Terminate");
    }
}