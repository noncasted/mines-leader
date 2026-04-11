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
        var opponent = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        opponent.Modifiers.Get(PlayerModifier.ManaCostPenalty).Returns(0f);

        var config = CardConfigs.Embargo;
        var result = new Embargo(opponent, config, roundService).Use();

        result.Result.HasError.Should().BeFalse();
        opponent.Modifiers.Received(1).Set(PlayerModifier.ManaCostPenalty, config.CostIncrease);
    }

    [Fact]
    public void Use_StacksWithExistingPenalty()
    {
        var opponent = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        opponent.Modifiers.Get(PlayerModifier.ManaCostPenalty).Returns(1f);

        var config = CardConfigs.Embargo;
        new Embargo(opponent, config, roundService).Use();

        opponent.Modifiers.Received(1).Set(PlayerModifier.ManaCostPenalty, 1f + config.CostIncrease);
    }

    [Fact]
    public void Use_SchedulesDisposeActionForNextRound()
    {
        var opponent = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        opponent.Modifiers.Get(PlayerModifier.ManaCostPenalty).Returns(0f);

        new Embargo(opponent, CardConfigs.Embargo, roundService).Use();

        roundService.Received(1).Schedule(Arg.Any<EmbargoDisposeAction>(), 1);
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var roundService = Substitute.For<IRoundActionService>();
        opponent.Modifiers.Get(PlayerModifier.ManaCostPenalty).Returns(0f);

        var result = new Embargo(opponent, CardConfigs.Embargo, roundService).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Embargo>().Subject;
        actionData.TargetPlayer.Should().Be(opponentId);
    }
}
