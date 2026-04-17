using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class EmbargoTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_SetsManaCostPenaltyOnOpponent()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.ManaCostPenalty, 0f } });
        var gameContext = MockGameContext(owner, opponent);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new Embargo(MockConfigs(), roundService, gameContext);

        var result = card.Use(owner, new CardUsePayload.Embargo { Type = CardType.Embargo });

        result.Result.HasError.Should().BeFalse();

        opponent.Modifiers.Received(1).Set(Arg.Any<MoveSnapshot>(), PlayerModifier.ManaCostPenalty,
            CardConfigs.Embargo.CostIncrease);
    }

    [Fact]
    public void Use_SchedulesDisposeAction()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.ManaCostPenalty, 0f } });
        var gameContext = MockGameContext(owner, opponent);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new Embargo(MockConfigs(), roundService, gameContext);

        card.Use(owner, new CardUsePayload.Embargo { Type = CardType.Embargo });

        roundService.Received(1).Schedule(Arg.Any<ModifierDisposeAction>(), 1);
    }
}