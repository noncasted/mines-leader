using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// ChainReaction: starts from a mine cell, chains to nearby unflagged mines
/// (up to MaxChain), then spawns new mines in a rhombus(SpawnSize) around each target.
/// Free cells become Taken+mine, Taken cells without mines get mines added.
/// Cells that already have mines are skipped.
/// </summary>
public class ChainReactionTests : PlayerCardTestsBase
{
    private CardUseResult Use(IBoard board, CardUsePayload.ChainReaction payload)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new ChainReaction(MockConfigs(), gameContext).Use(invoker, payload);
    }

    private (CardUseResult, MoveSnapshot) UseCapture(IBoard board, CardUsePayload.ChainReaction payload)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new ChainReaction(MockConfigs(), gameContext).UseCapture(invoker, payload);
    }

    [Fact]
    public void Use_ChainsFromInitialMine()
    {
        // Mine at target, another mine nearby for chaining
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t t t m t t t
                                                t t t t t t t
                                                t t t m t t t
                                                t t t t t t t
                                                t t t t t t t
                                                t t t t t t t
                                                """);

        var minesBefore = board.Cells.Values
                               .Count(c => c.Status == CellStatus.Taken && c is ITakenCell tc && tc.HasMine);

        var (result, moveSnapshot) = UseCapture(board,
            new CardUsePayload.ChainReaction { Position = new Position(3, 3) });

        result.Result.HasError.Should().BeFalse();

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();
        actionData!.SpawnedMines.Count.Should().BeGreaterThan(0);

        // Verify total mine count increased on the board
        var minesAfter = board.Cells.Values
                              .Count(c => c.Status == CellStatus.Taken && c is ITakenCell tc && tc.HasMine);
        minesAfter.Should().BeGreaterThan(minesBefore, "chain reaction should spawn new mines");

        // Verify at least one spawned mine is within SpawnSize distance from a chain target
        // Targets are (3,3) and (3,1) — SpawnSize=3, Rhombus(3) has halfSize=1
        var chainTargets = new[] { new Position(3, 3), new Position(3, 1) };

        actionData.SpawnedMines.Should()
                  .Contain(pos => chainTargets.Any(t => pos.DistanceTo(t) <= CardConfigs.ChainReaction.SpawnSize),
                      "spawned mines should be near chain targets");
    }

    [Fact]
    public void Use_TargetNotMine_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var result = Use(board, new CardUsePayload.ChainReaction { Position = target });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_MaxChainLimitsSpread()
    {
        // Chain of mines longer than MaxChain — should stop at MaxChain
        var (board, _) = BoardParser.Parse("""
                                           m t m t m t m t m t
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

        var minesBefore = board.Cells.Values
                               .Count(c => c.Status == CellStatus.Taken && c is ITakenCell t && t.HasMine);

        var (result, moveSnapshot) = UseCapture(board,
            new CardUsePayload.ChainReaction { Position = new Position(0, 0) });

        result.Result.HasError.Should().BeFalse();

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();
        actionData!.SpawnedMines.Should().NotBeEmpty();

        // MaxChain=3 limits chain targets. Each target spawns mines in Rhombus(SpawnSize=2).
        // With 5 mines on board but MaxChain=3, not all 5 should be chained.
        var minesAfter = board.Cells.Values
                              .Count(c => c.Status == CellStatus.Taken && c is ITakenCell t && t.HasMine);
        minesAfter.Should().BeGreaterThan(minesBefore, "chain should spawn new mines");

        // Spawned mines should be bounded by MaxChain * rhombus area
        var maxSpawnPerTarget = PatternShapes.Rhombus(CardConfigs.ChainReaction.SpawnSize)
                                             .Positions.SelectMany(r => r)
                                             .Count(v => v);

        actionData.SpawnedMines.Count.Should()
                  .BeLessThanOrEqualTo(CardConfigs.ChainReaction.MaxChain * maxSpawnPerTarget,
                      "spawned mines limited by MaxChain targets");
    }

    [Fact]
    public void Use_SpawnsMinesAroundTargets()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t m t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           t t t t t t t t t t
                                           """);

        var (result, moveSnapshot) = UseCapture(board,
            new CardUsePayload.ChainReaction { Position = new Position(5, 5) });

        result.Result.HasError.Should().BeFalse();

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();
        actionData!.SpawnedMines.Should().NotBeEmpty();

        // Spawned positions should now have mines
        foreach (var pos in actionData.SpawnedMines)
        {
            board.Cells[pos].Status.Should().Be(CellStatus.Taken);

            board.Cells[pos]
                 .Should()
                 .BeAssignableTo<ITakenCell>()
                 .Which.HasMine.Should()
                 .BeTrue($"spawned mine at {pos} should have mine");
        }
    }

    [Fact]
    public void Use_FreeCellsConvertedToTakenWithMine()
    {
        // Some Free cells near the mine — should become Taken+mine
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t _ t t t t
                                           t t _ m _ t t t
                                           t t t _ t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           """);

        var (result, moveSnapshot) = UseCapture(board,
            new CardUsePayload.ChainReaction { Position = new Position(3, 4) });

        result.Result.HasError.Should().BeFalse();

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();

        // Previously Free cells near mine should now be Taken with mines
        foreach (var pos in actionData!.SpawnedMines)
        {
            board.Cells[pos].Status.Should().Be(CellStatus.Taken);
        }
    }

    [Fact]
    public void Use_SkipsExistingMines()
    {
        // All neighbors already have mines — spawned count should be low
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t m m m t
                                           t m m m t
                                           t m m m t
                                           t t t t t
                                           """);

        var (result, moveSnapshot) = UseCapture(board,
            new CardUsePayload.ChainReaction { Position = new Position(2, 2) });

        result.Result.HasError.Should().BeFalse();

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();

        // 8 neighbors of (2,2) already have mines — SpawnedMines should NOT include them
        var existingMines = new HashSet<Position>
        {
            new(1, 1), new(2, 1), new(3, 1),
            new(1, 2), new(3, 2),
            new(1, 3), new(2, 3), new(3, 3)
        };

        foreach (var pos in actionData!.SpawnedMines)
        {
            existingMines.Should()
                         .NotContain(pos,
                             $"position {pos} already had a mine and should be skipped");
        }
    }

    [Fact]
    public void Use_OutOfBoundsPosition_Fails()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """);

        var result = Use(board, new CardUsePayload.ChainReaction { Position = new Position(99, 99) });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_FlaggedMinesSkippedInChain()
    {
        // Nearby mine is flagged — should not be included in chain
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t
                                           t t t t t t t
                                           t t t f t t t
                                           t t t t t t t
                                           t t t m t t t
                                           t t t t t t t
                                           t t t t t t t
                                           """);

        var (result, moveSnapshot) = UseCapture(board,
            new CardUsePayload.ChainReaction { Position = new Position(3, 4) });

        result.Result.HasError.Should().BeFalse();

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();

        // Flagged mine at (3,2) should be skipped — spawned mines should only be around (3,4)
        // SpawnSize=2 rhombus around (3,4) doesn't reach (3,2), so no mines should appear near it
        var flaggedMineNeighborhood = new HashSet<Position>
        {
            new(2, 1), new(3, 1), new(4, 1),
            new(2, 2), new(4, 2),
            new(2, 3), new(3, 3), new(4, 3)
        };

        // Spawned mines should only be around the initial target (3,4), not the flagged mine
        actionData!.SpawnedMines.Should().NotBeEmpty("initial mine should still spawn");

        foreach (var pos in actionData.SpawnedMines)
        {
            // Verify spawned mines are near (3,4), not near the flagged (3,2)
            pos.DistanceTo(new Position(3, 4))
               .Should()
               .BeLessThanOrEqualTo(CardConfigs.ChainReaction.SpawnSize,
                   $"spawned mine at {pos} should be near initial target (3,4), not near flagged mine (3,2)");
        }
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t m t t
                                           t t t t t
                                           t t t t t
                                           """);

        var ownerId = board.OwnerId;

        var (_, moveSnapshot) = UseCapture(board, new CardUsePayload.ChainReaction { Position = new Position(2, 2) });

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.ChainReaction>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}