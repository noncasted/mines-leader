using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class ManaFountainTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_RollsRangeAndGrantsMana()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        var gameRandom = Substitute.For<IGameRandom>();
        var config = CardConfigs.ManaFountain;
        gameRandom.Range(owner, config.MinMana, config.MaxMana).Returns(3);
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(1);

        var result = new ManaFountain(owner, config, roundService, gameRandom).Use();

        result.Result.HasError.Should().BeFalse();
        gameRandom.Received(1).Range(owner, config.MinMana, config.MaxMana);
        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMana, 3f);
        owner.Mana.Received(1).SetCurrent(4);
    }

    [Fact]
    public void Use_SchedulesDisposeActionForNextRound()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        var gameRandom = Substitute.For<IGameRandom>();
        var config = CardConfigs.ManaFountain;
        gameRandom.Range(owner, config.MinMana, config.MaxMana).Returns(2);
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(0);

        new ManaFountain(owner, config, roundService, gameRandom).Use();

        roundService.Received(1).Schedule(Arg.Any<ManaFountainDisposeAction>(), 1);
    }

    [Fact]
    public void Use_ActionDataIncludesRolledAmount()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var roundService = Substitute.For<IRoundActionService>();
        var gameRandom = Substitute.For<IGameRandom>();
        var config = CardConfigs.ManaFountain;
        gameRandom.Range(owner, config.MinMana, config.MaxMana).Returns(4);
        owner.Modifiers.Get(PlayerModifier.AdditionalMana).Returns(0f);
        owner.Mana.Current.Returns(0);

        var result = new ManaFountain(owner, config, roundService, gameRandom).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.ManaFountain>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
        actionData.RolledAmount.Should().Be(4);
    }
}
