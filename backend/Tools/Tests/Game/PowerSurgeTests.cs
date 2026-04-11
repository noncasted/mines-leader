using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class PowerSurgeTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_SetsAllCardsDiscountModifier()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AllCardsDiscount).Returns(0f);

        var config = CardConfigs.PowerSurge;
        var result = new PowerSurge(owner, config, roundService).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.AllCardsDiscount, config.Discount);
    }

    [Fact]
    public void Use_SchedulesDisposeActionForNextRound()
    {
        var owner = MockPlayer();
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AllCardsDiscount).Returns(0f);

        new PowerSurge(owner, CardConfigs.PowerSurge, roundService).Use();

        roundService.Received(1).Schedule(Arg.Any<PowerSurgeDisposeAction>(), 1);
    }

    [Fact]
    public void Use_DisposeActionRemovesDiscountAfterOneTick()
    {
        var owner = MockPlayer();
        var roundService = new RoundActionService();
        var config = CardConfigs.PowerSurge;
        owner.Modifiers.Get(PlayerModifier.AllCardsDiscount).Returns(config.Discount);

        new PowerSurge(owner, config, roundService).Use();
        roundService.Tick();

        owner.Modifiers.Received(1).Set(PlayerModifier.AllCardsDiscount, 0f);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var roundService = Substitute.For<IRoundActionService>();
        owner.Modifiers.Get(PlayerModifier.AllCardsDiscount).Returns(0f);

        var result = new PowerSurge(owner, CardConfigs.PowerSurge, roundService).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.PowerSurge>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
