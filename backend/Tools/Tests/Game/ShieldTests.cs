using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class ShieldTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_IncrementsShieldModifier()
    {
        var owner = MockPlayer();

        var result = new Shield().Use(owner, new CardUsePayload.Shield());

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Inc(PlayerModifier.Shield);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);

        var result = new Shield().Use(owner, new CardUsePayload.Shield());

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Shield>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
