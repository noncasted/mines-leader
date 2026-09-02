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

        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Is<IModifierSource>(s =>
            s.Type == PlayerModifier.AdditionalMoves &&
            s.Key == CoinToss.HeadsKey &&
            s.Value == CardConfigs.CoinToss.WinMoves));
        roundService.Received(1).Schedule(Arg.Any<ModifierRoundAction>());
    }

    [Fact]
    public void Use_Tails_SetsNegativeAdditionalMovesModifier()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMoves, 0f } });
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.FlipCoin(owner).Returns(false);
        var roundService = Substitute.For<IRoundActionService>();

        var card = new CoinToss(MockConfigs(), gameRandom, roundService);

        var result = card.Use(owner, new CardUsePayload.CoinToss { Type = CardType.CoinToss });

        result.Result.HasError.Should().BeFalse();

        owner.Moves.DidNotReceive().SetCurrent(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Is<IModifierSource>(s =>
            s.Type == PlayerModifier.AdditionalMoves &&
            s.Key == CoinToss.TailsKey &&
            s.Value == -CardConfigs.CoinToss.LoseMoves));
        roundService.Received(1).Schedule(Arg.Any<ModifierRoundAction>());
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
