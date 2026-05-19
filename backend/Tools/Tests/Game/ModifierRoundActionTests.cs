using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class ModifierRoundActionTests
{
    [Fact]
    public void Tick_SourceNotExpired_CallsUpdateAndReturnsFalse()
    {
        var player = Substitute.For<IPlayer>();
        var modifiers = Substitute.For<IModifiers>();
        player.Modifiers.Returns(modifiers);

        var source = Substitute.For<IModifierSource>();
        source.Tick().Returns(false);
        source.Id.Returns(Guid.NewGuid());

        var action = new ModifierRoundAction(player, source);
        var result = action.Tick(new MoveSnapshot());

        result.Should().BeFalse();
        modifiers.Received(1).Update(Arg.Any<MoveSnapshot>(), source);
        modifiers.DidNotReceive().Remove(Arg.Any<MoveSnapshot>(), Arg.Any<Guid>());
    }

    [Fact]
    public void Tick_SourceExpired_CallsRemoveAndReturnsTrue()
    {
        var player = Substitute.For<IPlayer>();
        var modifiers = Substitute.For<IModifiers>();
        player.Modifiers.Returns(modifiers);

        var sourceId = Guid.NewGuid();
        var source = Substitute.For<IModifierSource>();
        source.Tick().Returns(true);
        source.Id.Returns(sourceId);

        var action = new ModifierRoundAction(player, source);
        var result = action.Tick(new MoveSnapshot());

        result.Should().BeTrue();
        modifiers.Received(1).Remove(Arg.Any<MoveSnapshot>(), sourceId);
        modifiers.DidNotReceive().Update(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Tick_SourceTicksDownEachCall()
    {
        var player = Substitute.For<IPlayer>();
        var modifiers = Substitute.For<IModifiers>();
        player.Modifiers.Returns(modifiers);

        var source = new DurationModifierSource(PlayerModifier.Shield, 5f, "shield", 2);
        var action = new ModifierRoundAction(player, source);
        var snapshot = new MoveSnapshot();

        var firstResult = action.Tick(snapshot);

        firstResult.Should().BeFalse();
        modifiers.Received(1).Update(snapshot, source);
        modifiers.DidNotReceive().Remove(Arg.Any<MoveSnapshot>(), Arg.Any<Guid>());

        var secondResult = action.Tick(snapshot);

        secondResult.Should().BeTrue();
        modifiers.Received(1).Remove(snapshot, source.Id);
    }
}
