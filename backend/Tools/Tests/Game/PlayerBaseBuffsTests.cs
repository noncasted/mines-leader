using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class PlayerBaseBuffsTests : PlayerCardTestsBase
{
    [Fact]
    public void Grant_ResetsResourcesBeforeBuffs()
    {
        var player = MockPlayer();
        var snapshot = CreateSnapshot();
        var calls = new List<string>();

        player.Health.When(h => h.SetMax(snapshot, Arg.Any<int>())).Do(c => calls.Add($"health.max={c.Arg<int>()}"));
        player.Health.When(h => h.SetCurrent(snapshot, Arg.Any<int>())).Do(c => calls.Add($"health.current={c.Arg<int>()}"));
        player.Moves.When(m => m.SetMax(snapshot, Arg.Any<int>())).Do(c => calls.Add($"moves.max={c.Arg<int>()}"));
        player.Mana.When(m => m.SetMax(snapshot, Arg.Any<int>())).Do(c => calls.Add($"mana.max={c.Arg<int>()}"));
        player.Mana.When(m => m.SetCurrent(snapshot, Arg.Any<int>())).Do(c => calls.Add($"mana.current={c.Arg<int>()}"));
        player.Mana.When(m => m.Restore(snapshot)).Do(_ => calls.Add("mana.restore"));

        PlayerBaseBuffs.Grant(player, snapshot, health: 3, moves: 5, mana: 1);

        calls.Should().Equal(
            "health.max=0",
            "health.current=0",
            "moves.max=0",
            "mana.max=0",
            "mana.current=0",
            "health.max=3",
            "health.current=3",
            "moves.max=5",
            "mana.max=1",
            "mana.restore");
    }

    [Fact]
    public void Grant_AddsPermanentBaseModifiers()
    {
        var player = MockPlayer();
        var sources = new List<IModifierSource>();
        player.Modifiers.When(m => m.Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>()))
              .Do(call => sources.Add(call.Arg<IModifierSource>()));

        PlayerBaseBuffs.Grant(player, CreateSnapshot(), health: 3, moves: 5, mana: 1);

        sources.Select(s => (s.Type, s.Value, s.Key, s.TurnsToEnd)).Should().Equal(
            (PlayerModifier.BaseHealth, 3f, BaseHealthModifierSource.SourceKey, -1),
            (PlayerModifier.BaseMoves, 5f, BaseMovesModifierSource.SourceKey, -1),
            (PlayerModifier.BaseMana, 1f, BaseManaModifierSource.SourceKey, -1));
    }

    [Fact]
    public void BaseModifiers_NeverExpire()
    {
        var source = new BaseHealthModifierSource(3);

        source.Tick().Should().BeFalse();
        source.TurnsToEnd.Should().Be(-1);
    }

    [Fact]
    public void Grant_AppliedValuesAreNotAdditionalModifiers()
    {
        var player = MockPlayer();

        PlayerBaseBuffs.Grant(player, CreateSnapshot(), health: 3, moves: 5, mana: 1);

        player.Modifiers.DidNotReceive().Add(
            Arg.Any<MoveSnapshot>(),
            Arg.Is<IModifierSource>(s => s.Type == PlayerModifier.AdditionalHealth
                                      || s.Type == PlayerModifier.AdditionalMoves
                                      || s.Type == PlayerModifier.AdditionalMana));
    }
}
