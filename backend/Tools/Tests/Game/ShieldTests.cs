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

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.Shield, 0f }
        });

        var result = new Shield().Use(owner, new CardUsePayload.Shield());

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);

        var (_, snapshot) = new Shield().UseCapture(owner, new CardUsePayload.Shield());

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Shield>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}
