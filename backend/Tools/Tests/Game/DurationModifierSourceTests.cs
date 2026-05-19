using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

public class DurationModifierSourceTests
{
    [Fact]
    public void Tick_WithPositiveTurns_DecrementsAndReturnsFalse()
    {
        var source = new DurationModifierSource(PlayerModifier.Shield, 5f, "test-key", 3);

        var result = source.Tick();

        result.Should().BeFalse();
        source.TurnsToEnd.Should().Be(2);
    }

    [Fact]
    public void Tick_ReachesZero_ReturnsTrue()
    {
        var source = new DurationModifierSource(PlayerModifier.Shield, 5f, "test-key", 1);

        var result = source.Tick();

        result.Should().BeTrue();
        source.TurnsToEnd.Should().Be(0);
    }

    [Fact]
    public void Tick_AtZero_StaysZeroAndReturnsTrue()
    {
        var source = new DurationModifierSource(PlayerModifier.Shield, 5f, "test-key", 0);

        var result = source.Tick();

        result.Should().BeTrue();
        source.TurnsToEnd.Should().Be(0);
    }

    [Fact]
    public void Tick_Infinite_DoesNotDecrementAndReturnsFalse()
    {
        var source = new DurationModifierSource(PlayerModifier.Shield, 5f, "test-key", -1);

        var result = source.Tick();

        result.Should().BeFalse();
        source.TurnsToEnd.Should().Be(-1);
    }

    [Fact]
    public void GetOverview_ReturnsCorrectValues()
    {
        var source = new DurationModifierSource(PlayerModifier.AdditionalMoves, 2.5f, "move-boost", 4);

        var overview = source.GetOverview();

        overview.SourceId.Should().Be(source.Id);
        overview.Type.Should().Be(PlayerModifier.AdditionalMoves);
        overview.Value.Should().Be(2.5f);
        overview.Key.Should().Be("move-boost");
        overview.TurnsToEnd.Should().Be(4);
    }
}
