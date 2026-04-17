using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class CoinTossTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_Heads_SetsAdditionalMovesModifier()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMoves, 0f } });
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(true);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new CoinToss(MockConfigs(), gameRandom, roundService);

        var result = card.Use(owner, new CardUsePayload.CoinToss { Type = CardType.CoinToss });

        result.Result.HasError.Should().BeFalse();

        owner.Modifiers.Received(1).Set(Arg.Any<MoveSnapshot>(), PlayerModifier.AdditionalMoves,
            CardConfigs.CoinToss.WinMoves);
        roundService.Received(1).Schedule(Arg.Any<ModifierDisposeAction>(), 1);
    }

    [Fact]
    public void Use_Tails_RemovesLoseMoves()
    {
        var owner = MockPlayer();
        owner.Moves.Left.Returns(3);
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(false);

        var card = new CoinToss(MockConfigs(), gameRandom, Substitute.For<IRoundActionService>());

        var result = card.Use(owner, new CardUsePayload.CoinToss { Type = CardType.CoinToss });

        result.Result.HasError.Should().BeFalse();
        owner.Moves.Received(1).SetCurrent(Arg.Any<MoveSnapshot>(), 3 - CardConfigs.CoinToss.LoseMoves);
    }

    [Fact]
    public void Use_RecordsSnapshotWithIsHeads()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMoves, 0f } });
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(true);
        var snapshot = new MoveSnapshot();

        var card = new CoinToss(MockConfigs(), gameRandom, Substitute.For<IRoundActionService>());

        card.Use(owner, new CardUsePayload.CoinToss { Type = CardType.CoinToss }, snapshot);

        snapshot.Collect().Records.Should().ContainSingle(r => r is PlayerSnapshotRecord.CardUse);
    }
}