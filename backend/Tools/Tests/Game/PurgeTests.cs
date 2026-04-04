using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Purge: removes ALL effects from ALL cells on the owner's board.
/// Iterates every cell, copies effects list, removes each by ID.
/// Always succeeds (no-op if no effects present).
/// </summary>
public class PurgeTests
{
    private static IPlayer MockOwner(IBoard board)
    {
        var player = Substitute.For<IPlayer>();
        var user = Substitute.For<IUser>();
        var userId = Guid.NewGuid();
        user.Id.Returns(userId);
        player.User.Returns(user);
        player.Board.Returns(board);
        return player;
    }

    [Fact]
    public void Use_RemovesAllEffects()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );

        // Manually add effects to some cells
        var smokeEffect = new SmokeEffect { Id = Guid.NewGuid() };
        var fogEffect = new FogEffect { Id = Guid.NewGuid() };
        board.Cells[new Position(1, 1)].AddEffect(smokeEffect);
        board.Cells[new Position(2, 2)].AddEffect(fogEffect);

        var owner = MockOwner(board);

        var result = new Purge(owner).Use();

        result.Result.HasError.Should().BeFalse();

        // All effects should be removed
        board.Cells.Values
            .SelectMany(c => c.Effects)
            .Should()
            .BeEmpty("Purge should remove all effects from all cells");
    }

    [Fact]
    public void Use_NoEffects_Succeeds()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );

        var owner = MockOwner(board);

        var result = new Purge(owner).Use();

        result.Result.HasError.Should().BeFalse();
    }

    [Fact]
    public void Use_MixedEffectTypes_AllRemoved()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );

        // Add Smoke to Taken cell, Fog to Free cell
        var smokeEffect1 = new SmokeEffect { Id = Guid.NewGuid() };
        var smokeEffect2 = new SmokeEffect { Id = Guid.NewGuid() };
        var fogEffect = new FogEffect { Id = Guid.NewGuid() };
        board.Cells[new Position(2, 2)].AddEffect(smokeEffect1);
        board.Cells[new Position(3, 3)].AddEffect(smokeEffect2);
        board.Cells[new Position(0, 0)].AddEffect(fogEffect);

        var owner = MockOwner(board);

        var result = new Purge(owner).Use();

        result.Result.HasError.Should().BeFalse();

        board.Cells.Values
            .SelectMany(c => c.Effects)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Use_MultipleSmokeEffectsOnSameCell_AllRemoved()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );

        // Two separate effects on same cell
        var effect1 = new SmokeEffect { Id = Guid.NewGuid() };
        var effect2 = new SmokeEffect { Id = Guid.NewGuid() };
        board.Cells[new Position(2, 2)].AddEffect(effect1);
        board.Cells[new Position(2, 2)].AddEffect(effect2);

        var owner = MockOwner(board);

        new Purge(owner).Use();

        board.Cells[new Position(2, 2)].Effects.Should().BeEmpty();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );

        var owner = MockOwner(board);

        var result = new Purge(owner).Use();

        var snapshot = result.ActionData as CardActionSnapshot.Purge;
        snapshot.Should().NotBeNull();
        snapshot!.TargetPlayer.Should().Be(owner.User.Id);
    }

    [Fact]
    public void Use_AfterSmokeAndFog_BoardClean()
    {
        // Simulate Smoke and FogOfWar were played, then Purge clears everything
        var (board, _) = BoardParser.Parse("""
                                           t t t t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           t t t _ _ t t
                                           t t t _ t t t
                                           t t t t t t t
                                           t t t t t t t
                                           """
        );

        var smokeId = Guid.NewGuid();
        var fogId = Guid.NewGuid();

        // Add smoke to multiple cells
        foreach (var pos in new[] { new Position(1, 1), new Position(2, 1), new Position(1, 2) })
        {
            board.Cells[pos].AddEffect(new SmokeEffect { Id = smokeId });
        }

        // Add fog to free cells
        foreach (var pos in new[] { new Position(3, 3), new Position(4, 3) })
        {
            board.Cells[pos].AddEffect(new FogEffect { Id = fogId });
        }

        var totalEffects = board.Cells.Values.Sum(c => c.Effects.Count);
        totalEffects.Should().Be(5, "precondition: 5 effects added");

        var owner = MockOwner(board);
        new Purge(owner).Use();

        board.Cells.Values.Sum(c => c.Effects.Count).Should().Be(0);
    }
}