using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class AdrenalineTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_SetsAdditionalMovesModifier()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMoves, 0f } });
        var roundService = Substitute.For<IRoundActionService>();
        var card = new Adrenaline(MockConfigs(), roundService);

        var result = card.Use(owner, new CardUsePayload.Adrenaline { Type = CardType.Adrenaline });

        result.Result.HasError.Should().BeFalse();

        owner.Modifiers.Received(1).Set(Arg.Any<MoveSnapshot>(), PlayerModifier.AdditionalMoves,
            CardConfigs.Adrenaline.ExtraMoves);
    }

    [Fact]
    public void Use_SchedulesDisposeAction()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMoves, 0f } });
        var roundService = Substitute.For<IRoundActionService>();
        var card = new Adrenaline(MockConfigs(), roundService);

        card.Use(owner, new CardUsePayload.Adrenaline { Type = CardType.Adrenaline });

        roundService.Received(1).Schedule(Arg.Any<ModifierDisposeAction>(), 1);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMoves, 0f } });
        var card = new Adrenaline(MockConfigs(), Substitute.For<IRoundActionService>());

        var (_, snapshot) = card.UseCapture(owner, new CardUsePayload.Adrenaline { Type = CardType.Adrenaline });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Adrenaline>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}