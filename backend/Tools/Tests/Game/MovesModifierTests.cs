using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class MovesModifierTests
{
    private static (IPlayer player, Moves moves) MakePlayer(int max, int spent)
    {
        var modifiers = new Modifiers();
        var moves = new Moves(modifiers);
        var player = Substitute.For<IPlayer>();
        var user = Substitute.For<IUser>();
        user.Id.Returns(Guid.NewGuid());
        player.User.Returns(user);
        player.Moves.Returns(moves);
        player.Modifiers.Returns(modifiers);
        modifiers.BindOwner(player);
        moves.BindOwner(player);

        var snapshot = new MoveSnapshot();
        moves.SetMax(snapshot, max);
        moves.Restore(snapshot);

        for (var i = 0; i < spent; i++)
            moves.OnUsed(snapshot);

        return (player, moves);
    }

    [Fact]
    public void PositiveModifier_AddsOnTopOfWhatIsLeft()
    {
        var (player, moves) = MakePlayer(max: 5, spent: 1);

        player.Modifiers.Inc(new MoveSnapshot(), PlayerModifier.AdditionalMoves, 2f, "test", 1);

        moves.Left.Should().Be(6);
        moves.BaseMax.Should().Be(5);
        moves.ResultMax.Should().Be(7);
    }

    /// <summary>
    /// Дебафф режет потолок, а не уже доступный ход: при 4 из 5 после -1 остаётся 4 из 4.
    /// </summary>
    [Fact]
    public void NegativeModifier_OnlyCapsThePool_WhenThereIsSlack()
    {
        var (player, moves) = MakePlayer(max: 5, spent: 1);

        player.Modifiers.Inc(new MoveSnapshot(), PlayerModifier.AdditionalMoves, -1f, "test", 1);

        moves.Left.Should().Be(4);
        moves.ResultMax.Should().Be(4);
    }

    [Fact]
    public void NegativeModifier_TakesAMove_WhenThePoolIsFull()
    {
        var (player, moves) = MakePlayer(max: 5, spent: 0);

        player.Modifiers.Inc(new MoveSnapshot(), PlayerModifier.AdditionalMoves, -1f, "test", 1);

        moves.Left.Should().Be(4);
        moves.ResultMax.Should().Be(4);
    }

    [Fact]
    public void NegativeModifier_KeepsSpendingDownToZero()
    {
        var (player, moves) = MakePlayer(max: 5, spent: 1);
        var snapshot = new MoveSnapshot();

        player.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMoves, -1f, "test", 1);

        for (var i = 0; i < 4; i++)
            moves.OnUsed(snapshot);

        moves.Left.Should().Be(0);
    }

    [Fact]
    public void PositiveModifier_IsSpendableDownToZero()
    {
        var (player, moves) = MakePlayer(max: 5, spent: 0);
        var snapshot = new MoveSnapshot();

        player.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMoves, 2f, "test", 1);

        for (var i = 0; i < 7; i++)
            moves.OnUsed(snapshot);

        moves.Left.Should().Be(0);
    }

    [Fact]
    public void ResultMax_NeverGoesBelowZero()
    {
        var (player, moves) = MakePlayer(max: 5, spent: 0);

        player.Modifiers.Inc(new MoveSnapshot(), PlayerModifier.AdditionalMoves, -9f, "test", 1);

        moves.ResultMax.Should().Be(0);
        moves.Left.Should().Be(0);
    }
}
