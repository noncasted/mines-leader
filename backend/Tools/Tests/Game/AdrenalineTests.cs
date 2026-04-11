using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class AdrenalineTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_AddsExtraMovesToCurrent()
    {
        var owner = MockPlayer();
        owner.Moves.Left.Returns(2);

        var result = new Adrenaline(owner, CardConfigs.Adrenaline).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Moves.Received(1).SetCurrent(2 + CardConfigs.Adrenaline.ExtraMoves);
    }

    [Fact]
    public void Use_ZeroMovesLeft_StillAddsExtraMoves()
    {
        var owner = MockPlayer();
        owner.Moves.Left.Returns(0);

        new Adrenaline(owner, CardConfigs.Adrenaline).Use();

        owner.Moves.Received(1).SetCurrent(CardConfigs.Adrenaline.ExtraMoves);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        owner.Moves.Left.Returns(1);

        var result = new Adrenaline(owner, CardConfigs.Adrenaline).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Adrenaline>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
