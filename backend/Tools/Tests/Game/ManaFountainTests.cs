using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class ManaFountainTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_RollsRangeAndGrantsMana()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMana, 0f } });
        owner.Mana.Current.Returns(1);
        var gameRandom = Substitute.For<IGameRandom>();
        var config = CardConfigs.ManaFountain;
        gameRandom.Range(owner, config.MinMana, config.MaxMana).Returns(3);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new ManaFountain(MockConfigs(), roundService, gameRandom);

        var result = card.Use(owner, new CardUsePayload.ManaFountain { Type = CardType.ManaFountain });

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
        owner.Mana.Received(1).SetCurrent(Arg.Any<MoveSnapshot>(), 4);
    }

    [Fact]
    public void Use_SchedulesDisposeAction()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
            { { PlayerModifier.AdditionalMana, 0f } });
        owner.Mana.Current.Returns(0);
        var gameRandom = Substitute.For<IGameRandom>();
        gameRandom.Range(owner, Arg.Any<int>(), Arg.Any<int>()).Returns(2);
        var roundService = Substitute.For<IRoundActionService>();
        var card = new ManaFountain(MockConfigs(), roundService, gameRandom);

        card.Use(owner, new CardUsePayload.ManaFountain { Type = CardType.ManaFountain });

        roundService.Received(1).Schedule(Arg.Any<ModifierRoundAction>());
    }
}
