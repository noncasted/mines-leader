using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class CoinTossTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_Heads_AddsWinMoves()
    {
        var owner = MockPlayer();
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(true);
        owner.Moves.Left.Returns(1);

        var config = CardConfigs.CoinToss;
        var result = new CoinToss(owner, config, gameRandom).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Moves.Received(1).SetCurrent(1 + config.WinMoves);
    }

    [Fact]
    public void Use_Tails_RemovesLoseMoves()
    {
        var owner = MockPlayer();
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(false);
        owner.Moves.Left.Returns(3);

        var config = CardConfigs.CoinToss;
        var result = new CoinToss(owner, config, gameRandom).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Moves.Received(1).SetCurrent(3 - config.LoseMoves);
    }

    [Fact]
    public void Use_Tails_MovesClampedToZero()
    {
        var owner = MockPlayer();
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(false);
        owner.Moves.Left.Returns(0);

        var config = CardConfigs.CoinToss;
        new CoinToss(owner, config, gameRandom).Use();

        owner.Moves.Received(1).SetCurrent(0);
    }

    [Fact]
    public void Use_ActionDataIncludesIsHeads()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(true);
        owner.Moves.Left.Returns(1);

        var result = new CoinToss(owner, CardConfigs.CoinToss, gameRandom).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.CoinToss>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
        actionData.IsHeads.Should().BeTrue();
    }
}
