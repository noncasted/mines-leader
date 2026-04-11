using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class BloodPactTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_TakesDamageAndGrantsManaAndMoves()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(1);
        owner.Moves.Left.Returns(2);

        var config = CardConfigs.BloodPact;
        var result = new BloodPact(owner, config, roundService).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Health.Received(1).TakeDamage(config.HpCost);
        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMana, config.ManaGain);
        owner.Mana.Received(1).SetCurrent(1 + config.ManaGain);
        owner.Moves.Received(1).SetCurrent(2 + config.ExtraMoves);
    }

    [Fact]
    public void Use_SchedulesDisposeActionForNextRound()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(0);
        owner.Moves.Left.Returns(0);

        new BloodPact(owner, CardConfigs.BloodPact, roundService).Use();

        roundService.Received(1).Schedule(Arg.Any<BloodPactDisposeAction>(), 1);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(0);
        owner.Moves.Left.Returns(0);

        var result = new BloodPact(owner, CardConfigs.BloodPact, roundService).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.BloodPact>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
