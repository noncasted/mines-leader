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
        owner.Modifiers.Get(PlayerModifier.NextCardDiscount).Returns(0f);

        var config = CardConfigs.Focus;
        var result = new Focus(owner, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.NextCardDiscount, config.Discount);
    }

    [Fact]
    public void Use_StacksWithExistingDiscount()
    {
        var owner = MockPlayer();
        owner.Modifiers.Get(PlayerModifier.NextCardDiscount).Returns(2f);

        var config = CardConfigs.Focus;
        new Focus(owner, config).Use();

        owner.Modifiers.Received(1).Set(PlayerModifier.NextCardDiscount, 2f + config.Discount);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        owner.Modifiers.Get(PlayerModifier.NextCardDiscount).Returns(0f);

        var result = new Focus(owner, CardConfigs.Focus).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Focus>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
