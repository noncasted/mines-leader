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
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AllCardsDiscount, 0f } });
        var roundService = Substitute.For<IRoundActionService>();
        var card = new PowerSurge(MockConfigs(), roundService);

        var result = card.Use(owner, new CardUsePayload.PowerSurge { Type = CardType.PowerSurge });

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.AllCardsDiscount, CardConfigs.PowerSurge.Discount);
    }

    [Fact]
    public void Use_SchedulesDisposeAction()
    {
        var owner = MockPlayer();
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AllCardsDiscount, 0f } });
        var roundService = Substitute.For<IRoundActionService>();
        var card = new PowerSurge(MockConfigs(), roundService);

        card.Use(owner, new CardUsePayload.PowerSurge { Type = CardType.PowerSurge });

        roundService.Received(1).Schedule(Arg.Any<ModifierDisposeAction>(), 1);
    }

    [Fact]
    public void Use_DisposeActionRemovesDiscountAfterOneTick()
    {
        var owner = MockPlayer();
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AllCardsDiscount, CardConfigs.PowerSurge.Discount } });
        var roundService = new RoundActionService();
        var card = new PowerSurge(MockConfigs(), roundService);

        card.Use(owner, new CardUsePayload.PowerSurge { Type = CardType.PowerSurge });
        roundService.Tick();

        owner.Modifiers.Received(1).Set(PlayerModifier.AllCardsDiscount, 0f);
    }
}
