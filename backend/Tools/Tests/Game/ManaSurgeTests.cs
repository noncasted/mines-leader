using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class ManaSurgeTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_SetsAdditionalManaModifierAndCurrentMana()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(2);

        var result = new ManaSurge(owner, CardConfigs.ManaSurge, roundService).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMana, CardConfigs.ManaSurge.ManaGain);
        owner.Mana.Received(1).SetCurrent(2 + CardConfigs.ManaSurge.ManaGain);
    }

    [Fact]
    public void Use_SchedulesDisposeActionForNextRound()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(0);

        new ManaSurge(owner, CardConfigs.ManaSurge, roundService).Use();

        roundService.Received(1).Schedule(Arg.Any<ManaSurgeDisposeAction>(), 1);
    }

    [Fact]
    public void Use_DisposeActionRemovesModifierAfterOneTick()
    {
        var owner = MockPlayer();
        var roundService = new RoundActionService();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(CardConfigs.ManaSurge.ManaGain);
        owner.Mana.Current.Returns(0);

        new ManaSurge(owner, CardConfigs.ManaSurge, roundService).Use();
        roundService.Tick();

        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMana, 0f);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(0);

        var result = new ManaSurge(owner, CardConfigs.ManaSurge, roundService).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.ManaSurge>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
