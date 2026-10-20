using Common.Reactive;
using FluentAssertions;
using Xunit;

namespace Tests.Reactive;

/// <summary>
/// F2.42: a TerminatedLifetime behaves as an ended Lifetime (listeners run at once, the token is cancelled), and the
/// value/entry lifetime handed out by View is bounded by the subscriber lifetime.
/// </summary>
public class LifetimeBoundsTests
{
    [Fact]
    public void TerminatedLifetime_Listen_InvokesImmediately()
    {
        var lifetime = new TerminatedLifetime();
        var calls = 0;

        lifetime.Listen(() => calls++);

        calls.Should().Be(1);
    }

    [Fact]
    public void TerminatedLifetime_Token_IsCancelled()
    {
        new TerminatedLifetime().Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void TerminatedLifetime_ListenNull_Throws()
    {
        var act = () => new TerminatedLifetime().Listen(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TerminatedLifetime_Advise_DoesNotDeliver()
    {
        var source = new EventSource<int>();
        var calls = 0;

        source.Advise(new TerminatedLifetime(), _ => calls++);
        source.Invoke(1);

        calls.Should().Be(0);
        source.ListenersCount.Should().Be(0);
    }

    [Fact]
    public void TerminatedLifetime_Child_IsTerminated()
    {
        var child = new TerminatedLifetime().Child();

        child.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void PropertyView_ValueLifetime_EndsWithSubscriber()
    {
        var property = new ViewableProperty<int>(1);
        var subscriber = new Lifetime();
        IReadOnlyLifetime? viewed = null;

        property.View(subscriber, (valueLifetime, _) => viewed = valueLifetime);

        viewed!.IsTerminated.Should().BeFalse();

        subscriber.Terminate();

        viewed.IsTerminated.Should().BeTrue();
        property.ValueLifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void PropertyView_ValueLifetime_EndsWithValue()
    {
        var property = new ViewableProperty<int>(1);
        var subscriber = new Lifetime();
        var viewed = new List<IReadOnlyLifetime>();

        property.View(subscriber, (valueLifetime, _) => viewed.Add(valueLifetime));
        property.Set(2);

        viewed.Should().HaveCount(2);
        viewed[0].IsTerminated.Should().BeTrue();
        viewed[1].IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void PropertyView_SubscriptionMadeInValueLifetime_EndsWithSubscriber()
    {
        var property = new ViewableProperty<int>(1);
        var inner = new EventSource<int>();
        var subscriber = new Lifetime();
        var calls = 0;

        property.View(subscriber, (valueLifetime, _) => inner.Advise(valueLifetime, _ => calls++));
        subscriber.Terminate();
        inner.Invoke(1);

        calls.Should().Be(0);
        inner.ListenersCount.Should().Be(0);
    }

    [Fact]
    public void PropertyView_ChangesAfterEnd_LeaveNoListenerOnSubscriber()
    {
        var property = new ViewableProperty<int>(0);
        var subscriber = new CountingLifetime();

        property.View(subscriber, (_, _) => { });

        for (var i = 1; i <= 10; i++)
            property.Set(i);

        // The subscription itself and the intersection of the current value: earlier intersections are gone.
        subscriber.Listeners.Should().Be(2);
    }

    [Fact]
    public void PropertyViewNotNull_ValueLifetime_EndsWithSubscriber()
    {
        var property = new ViewableProperty<string>("a");
        var subscriber = new Lifetime();
        IReadOnlyLifetime? viewed = null;

        property.ViewNotNull(subscriber, (valueLifetime, _) => viewed = valueLifetime);
        subscriber.Terminate();

        viewed!.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void ListView_EntryLifetime_EndsWithSubscriber_ListEntryStays()
    {
        var list = new ViewableList<string>();
        list.Add("existing");
        var subscriber = new Lifetime();
        var viewed = new Dictionary<string, IReadOnlyLifetime>();

        list.View(subscriber, (entryLifetime, value) => viewed[value] = entryLifetime);
        list.Add("added");
        subscriber.Terminate();

        viewed["existing"].IsTerminated.Should().BeTrue();
        viewed["added"].IsTerminated.Should().BeTrue();
        list.GetLifetime("existing").IsTerminated.Should().BeFalse();
        list.GetLifetime("added").IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void ListView_EntryLifetime_EndsWithRemove()
    {
        var list = new ViewableList<string>();
        var subscriber = new Lifetime();
        IReadOnlyLifetime? viewed = null;

        list.View(subscriber, (entryLifetime, _) => viewed = entryLifetime);
        list.Add("a");
        list.Remove("a");

        viewed!.IsTerminated.Should().BeTrue();
        subscriber.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void DictionaryView_EntryLifetime_EndsWithSubscriber()
    {
        var dictionary = new ViewableDictionary<int, string>();
        dictionary.Add(1, "existing");
        var subscriber = new Lifetime();
        var viewed = new List<IReadOnlyLifetime>();

        dictionary.View(subscriber, (IReadOnlyLifetime entryLifetime, int _, string _) => viewed.Add(entryLifetime));
        dictionary.View(subscriber, (IReadOnlyLifetime entryLifetime, string _) => viewed.Add(entryLifetime));
        dictionary.Add(2, "added");
        subscriber.Terminate();

        viewed.Should().HaveCount(4).And.OnlyContain(lifetime => lifetime.IsTerminated);
        dictionary.GetLifetime(1).IsTerminated.Should().BeFalse();
        dictionary.GetLifetime(2).IsTerminated.Should().BeFalse();
    }

    // Counts the live listeners: shows whether the intersections leave the subscriber lifetime.
    private class CountingLifetime : IReadOnlyLifetime
    {
        private readonly Lifetime _inner = new();

        public int Listeners { get; private set; }

        public CancellationToken Token => _inner.Token;
        public bool IsTerminated => _inner.IsTerminated;

        public void Listen(Action callback)
        {
            Listeners++;
            _inner.Listen(callback);
        }

        public void RemoveListener(Action callback)
        {
            Listeners--;
            _inner.RemoveListener(callback);
        }
    }
}
