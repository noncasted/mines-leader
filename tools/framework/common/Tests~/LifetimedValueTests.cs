using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

public class LifetimedValueTests
{
    [Fact]
    public void DefaultOfValueType_HasValueLifetime()
    {
        var flag = new ViewableProperty<bool>(false);
        var number = new ViewableProperty<int>();

        flag.ValueLifetime.Should().NotBeNull();
        number.ValueLifetime.Should().NotBeNull();
        flag.ValueLifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void View_DefaultOfValueType_GetsLiveLifetime()
    {
        var lifetime = new Lifetime();
        var property = new ViewableProperty<bool>(false);
        IReadOnlyLifetime? viewed = null;

        property.View(lifetime, (valueLifetime, _) => viewed ??= valueLifetime);

        viewed.Should().NotBeNull();
        viewed!.IsTerminated.Should().BeFalse();

        property.Set(true);

        viewed.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Set_Equal_DoesNotNotify_KeepsValueLifetime()
    {
        var property = new ViewableProperty<int>(1);
        var initial = property.ValueLifetime;
        var calls = 0;

        property.Advise(new Lifetime(), (_, _) => calls++);
        property.Set(1);

        calls.Should().Be(0);
        initial.IsTerminated.Should().BeFalse();
        property.ValueLifetime.Should().BeSameAs(initial);
    }

    // Null over null is an equal value too: what was subscribed in the lifetime of the empty value stays.
    [Fact]
    public void Set_NullOverNull_DoesNotNotify_KeepsValueLifetime()
    {
        var property = new ViewableProperty<string?>(null);
        var initial = property.ValueLifetime;
        var calls = 0;

        property.Advise(new Lifetime(), (_, _) => calls++);
        property.Set(null);

        calls.Should().Be(0);
        initial.IsTerminated.Should().BeFalse();
        property.ValueLifetime.Should().BeSameAs(initial);
    }

    [Fact]
    public void Set_NullOverValue_And_ValueOverNull_Notify()
    {
        var property = new ViewableProperty<string?>("a");
        var seen = new List<string?>();

        property.Advise(new Lifetime(), (_, value) => seen.Add(value));
        property.Set(null);
        property.Set("b");

        seen.Should().Equal(null, "b");
    }

    [Fact]
    public void Set_Changed_NotifiesAndRenewsValueLifetime()
    {
        var property = new ViewableProperty<int>(1);
        var initial = property.ValueLifetime;
        var seen = new List<int>();

        property.Advise(new Lifetime(), (_, value) => seen.Add(value));
        property.Set(2);

        seen.Should().Equal(2);
        initial.IsTerminated.Should().BeTrue();
        property.ValueLifetime.IsTerminated.Should().BeFalse();
    }

    // The value changed in place: the same value comes again, with a new lifetime.
    [Fact]
    public void Notify_RenewsValueLifetime_NotifiesWithCurrentValue()
    {
        var property = new ViewableProperty<int>(1);
        var initial = property.ValueLifetime;
        var seen = new List<int>();

        property.Advise(new Lifetime(), (_, value) => seen.Add(value));
        property.Notify();

        seen.Should().Equal(1);
        initial.IsTerminated.Should().BeTrue();
        property.ValueLifetime.Should().NotBeSameAs(initial);
    }

    [Fact]
    public void InvokeAdvices_NotifiesWithCurrentValue_KeepsValueLifetime()
    {
        var property = new ViewableProperty<int>(1);
        var initial = property.ValueLifetime;
        var seen = new List<(IReadOnlyLifetime, int)>();

        property.Advise(new Lifetime(), (lifetime, value) => seen.Add((lifetime, value)));
        property.InvokeAdvices();

        seen.Should().Equal((initial, 1));
        property.ValueLifetime.Should().BeSameAs(initial);
        initial.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void Dispose_EndsValueLifetime_DropsListeners()
    {
        var property = new ViewableProperty<int>(1);
        var initial = property.ValueLifetime;
        var calls = 0;

        property.Advise(new Lifetime(), (_, _) => calls++);
        property.Dispose();
        property.Set(2);

        initial.IsTerminated.Should().BeTrue();
        calls.Should().Be(0);
    }

    [Fact]
    public void AddClampMax_ListenersNeverSeeValueOverMax()
    {
        var lifetime = new Lifetime();
        var property = new ViewableProperty<int>(8);
        var seen = new List<int>();

        property.Advise(lifetime, (_, value) => seen.Add(value));

        property.AddClampMax(5, 10).Should().Be(10);
        property.IncreaseClampMax(10).Should().Be(10);
        property.Value.Should().Be(10);

        seen.Should().Equal(10);
    }

    [Fact]
    public void IncreaseClampMax_BelowMax_Increases()
    {
        var property = new ViewableProperty<int>(3);

        property.IncreaseClampMax(10).Should().Be(4);
        property.Value.Should().Be(4);
    }

    [Fact]
    public void AdviseTrue_ViewTrue_FollowTheFlag()
    {
        var property = new ViewableProperty<bool>(false);
        var lifetime = new Lifetime();
        var advised = 0;
        var trueLifetimes = new List<IReadOnlyLifetime>();

        property.AdviseTrue(lifetime, () => advised++);
        property.ViewTrue(lifetime, trueLifetimes.Add);

        property.Set(true);
        property.Set(false);
        property.Set(true);

        advised.Should().Be(2);
        trueLifetimes.Should().HaveCount(2);
        trueLifetimes[0].IsTerminated.Should().BeTrue();
        trueLifetimes[1].IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void Set_InsideHandler_LaterHandlersSeeOnlyTheNewestValue()
    {
        var lifetime = new Lifetime();
        var property = new ViewableProperty<int>(0);
        var seen = new List<(int Value, bool IsTerminated)>();

        property.Advise(lifetime, (_, value) => {
            if (value == 1)
                property.Set(2);
        });

        property.Advise(lifetime, (valueLifetime, value) => seen.Add((value, valueLifetime.IsTerminated)));

        property.Set(1);

        // The nested Set already delivered 2; the outer pass must not deliver the superseded 1 after it.
        seen.Should().Equal((2, false));
        property.Value.Should().Be(2);
    }

    [Fact]
    public void ViewTrue_NotifyWhileTrue_EndsThePreviousTrueLifetime()
    {
        var lifetime = new Lifetime();
        var property = new ViewableProperty<bool>(true);
        var lifetimes = new List<IReadOnlyLifetime>();

        property.ViewTrue(lifetime, trueLifetime => lifetimes.Add(trueLifetime));
        property.Notify();

        lifetimes.Should().HaveCount(2);
        lifetimes[0].IsTerminated.Should().BeTrue();
        lifetimes[1].IsTerminated.Should().BeFalse();
    }
}
