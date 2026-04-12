using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class BloodPactTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_TakesDamageAndGrantsManaAndMoves()
    {
        var owner = MockPlayer();
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMana, 0f }, { PlayerModifier.AdditionalMoves, 0f } });
        owner.Mana.Current.Returns(1);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new BloodPact(MockConfigs(), roundService);

        var result = card.Use(owner, new CardUsePayload.BloodPact { Type = CardType.BloodPact });

        result.Result.HasError.Should().BeFalse();
        owner.Health.Received(1).TakeDamage(CardConfigs.BloodPact.HpCost);
        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMana, CardConfigs.BloodPact.ManaGain);
        owner.Mana.Received(1).SetCurrent(1 + CardConfigs.BloodPact.ManaGain);
        owner.Modifiers.Received(1).Set(PlayerModifier.AdditionalMoves, CardConfigs.BloodPact.ExtraMoves);
    }

    [Fact]
    public void Use_SchedulesDisposeActions()
    {
        var owner = MockPlayer();
        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMana, 0f }, { PlayerModifier.AdditionalMoves, 0f } });
        owner.Mana.Current.Returns(0);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new BloodPact(MockConfigs(), roundService);

        card.Use(owner, new CardUsePayload.BloodPact { Type = CardType.BloodPact });

        roundService.Received(2).Schedule(Arg.Any<ModifierDisposeAction>(), 1);
    }
}
