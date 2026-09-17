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
            (PlayerModifier.BaseMana, 1f, BaseManaModifierSource.SourceKey, -1),
            (PlayerModifier.BaseManaAddPerRound, 0f, BaseManaAddPerRoundModifierSource.SourceKey, -1));
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

    [Fact]
    public void GrowMana_RaisesBaseMaxAndBuffValue()
    {
        var player = MockPlayer();
        var snapshot = CreateSnapshot();
        var source = new BaseManaAddPerRoundModifierSource();
        player.Modifiers.Sources.Returns(new List<IModifierSource> { source });
        player.Mana.BaseMax.Returns(3);

        PlayerBaseBuffs.GrowMana(player, snapshot, cap: 10);

        // AdditionalMana modifiers expire after the growth; growing from ResultMax
        // would bake the temporary bonus into BaseMax permanently.
        player.Mana.Received(1).SetMax(snapshot, 4);
        source.Value.Should().Be(1f);
        player.Modifiers.Received(1).Update(snapshot, source);
    }

    [Fact]
    public void GrowMana_AtCap_KeepsBuffValue()
    {
        var player = MockPlayer();
        var snapshot = CreateSnapshot();
        var source = new BaseManaAddPerRoundModifierSource();
        player.Modifiers.Sources.Returns(new List<IModifierSource> { source });
        player.Mana.BaseMax.Returns(10);

        PlayerBaseBuffs.GrowMana(player, snapshot, cap: 10);

        player.Mana.DidNotReceive().SetMax(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
        source.Value.Should().Be(0f);
        player.Modifiers.DidNotReceive().Update(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }
}
