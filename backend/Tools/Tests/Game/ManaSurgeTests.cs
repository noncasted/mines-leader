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
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMana, 0f } });
        owner.Mana.Current.Returns(2);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new ManaSurge(MockConfigs(), roundService);

        var result = card.Use(owner, new CardUsePayload.ManaSurge { Type = CardType.ManaSurge });

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMana, CardConfigs.ManaSurge.ManaGain);
        owner.Mana.Received(1).SetCurrent(2 + CardConfigs.ManaSurge.ManaGain);
    }

    [Fact]
    public void Use_SchedulesDisposeAction()
    {
        var owner = MockPlayer();
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMana, 0f } });
        owner.Mana.Current.Returns(0);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new ManaSurge(MockConfigs(), roundService);

        card.Use(owner, new CardUsePayload.ManaSurge { Type = CardType.ManaSurge });

        roundService.Received(1).Schedule(Arg.Any<ModifierDisposeAction>(), 1);
    }
}
