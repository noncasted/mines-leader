using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class FocusTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_SetsNextCardDiscountModifier()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.NextCardDiscount, 0f } });
        var card = new Focus(MockConfigs(), Substitute.For<IRoundActionService>());

        var result = card.Use(owner, new CardUsePayload.Focus { Type = CardType.Focus });

        result.Result.HasError.Should().BeFalse();

        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_SchedulesDisposeAction()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.NextCardDiscount, 0f } });
        var roundService = Substitute.For<IRoundActionService>();
        var card = new Focus(MockConfigs(), roundService);

        card.Use(owner, new CardUsePayload.Focus { Type = CardType.Focus });

        roundService.Received(1).Schedule(Arg.Any<ModifierRoundAction>());
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.NextCardDiscount, 0f } });
        var card = new Focus(MockConfigs(), Substitute.For<IRoundActionService>());

        var (_, snapshot) = card.UseCapture(owner, new CardUsePayload.Focus { Type = CardType.Focus });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Focus>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}
