using System;
using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

// The reactive primitives on the paths of every frame allocate nothing once warmed up: on WebGL (Boehm, not
// incremental) every collection stops the whole game.
public class FrameAllocationTests
{
    private const int Passes = 1000;

    [Fact]
    public void LifetimedValue_SetWithoutListeners_AllocatesNothing()
    {
        var property = new ViewableProperty<int>();

        Measure(() => property.Set(property.Value + 1)).Should().Be(0);
    }

    [Fact]
    public void LifetimedValue_ValueLifetimeAfterChange_IsNewAndAlive()
    {
        var property = new ViewableProperty<int>();
        var first = property.ValueLifetime;

        property.Set(1);

        first.IsTerminated.Should().BeTrue();
        property.ValueLifetime.Should().NotBeSameAs(first);
        property.ValueLifetime.IsTerminated.Should().BeFalse();
        property.ValueLifetime.Should().BeSameAs(property.ValueLifetime);
    }

    [Fact]
    public void LifetimedValue_Disposed_ValueLifetimeEnded()
    {
        var property = new ViewableProperty<int>();

        property.Dispose();

        property.ValueLifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void ViewableDictionary_ForeachOverValues_AllocatesNothing()
    {
        var dictionary = new ViewableDictionary<int, string>();
        dictionary.Add(1, "a");
        dictionary.Add(2, "b");
        var length = 0;

        var bytes = Measure(() => {
            foreach (var value in dictionary.Values)
                length += value.Length;
        });

        bytes.Should().Be(0);
        length.Should().Be(2 * (Passes + 1));
    }

    private static long Measure(Action pass)
    {
        pass.Invoke();

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < Passes; i++)
            pass.Invoke();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
