using Common.Reactive;
using FluentAssertions;
using Xunit;

namespace Tests.Reactive;

/// <summary>
/// F8.5: entries of the viewable collections and values of the viewable property - their lifetimes and events.
/// </summary>
public class ReactiveCollectionsTests
{
    [Fact]
    public void List_Remove_EndsEntryLifetime_AfterEntryLeftTheList()
    {
        var list = new ViewableList<string>();
        var lifetime = list.Add("a");
        var countAtEnd = -1;
        lifetime.Listen(() => countAtEnd = list.Count);

        list.Remove("a");

        lifetime.IsTerminated.Should().BeTrue();
        countAtEnd.Should().Be(0);
    }

    [Fact]
    public void List_RemovedEntryAddedAgain_GetsNewLifetime()
    {
        var list = new ViewableList<string>();
        var first = list.Add("a");
        list.Remove("a");

        var second = list.Add("a");

        first.IsTerminated.Should().BeTrue();
        second.IsTerminated.Should().BeFalse();
        list.GetLifetime("a").Should().BeSameAs(second);
    }

    [Fact]
    public void List_RemoveUnknown_Throws()
    {
        var list = new ViewableList<string>();

        var act = () => list.Remove("missing");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void List_Clear_EmptiesAndEndsEveryEntry()
    {
        var list = new ViewableList<string>();
        var a = list.Add("a");
        var b = list.Add("b");

        list.Clear();

        list.Should().BeEmpty();
        a.IsTerminated.Should().BeTrue();
        b.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void List_AfterDispose_AddReturnsTerminatedLifetime_ListenRunsAtOnce()
    {
        var list = new ViewableList<string>();
        list.Dispose();
        var called = false;

        var lifetime = list.Add("a");
        lifetime.Listen(() => called = true);

        lifetime.IsTerminated.Should().BeTrue();
        called.Should().BeTrue();
        list.Should().BeEmpty();
    }

    [Fact]
    public void List_View_SeesExistingAndAdded_StopsWithSubscriber()
    {
        var list = new ViewableList<string>();
        list.Add("a");
        var subscriber = new Lifetime();
        var seen = new List<string>();

        list.View(subscriber, value => seen.Add(value));
        list.Add("b");
        subscriber.Terminate();
        list.Add("c");

        seen.Should().Equal("a", "b");
    }

    [Fact]
    public void Dictionary_RemovedKeyAddedAgain_GetsNewLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var first = dictionary.Add(1, "a");
        dictionary.Remove(1);

        var second = dictionary.Add(1, "b");

        first.IsTerminated.Should().BeTrue();
        second.IsTerminated.Should().BeFalse();
        dictionary.GetLifetime(1).Should().BeSameAs(second);
    }

    [Fact]
    public void Dictionary_RemoveUnknown_Throws()
    {
        var dictionary = new ViewableDictionary<int, string>();

        var act = () => dictionary.Remove(1);

        act.Should().Throw<KeyNotFoundException>();
    }

    // F2.43: Dispose ends the entry lifetimes, as Remove does for one entry.
    [Fact]
    public void Dictionary_Dispose_EndsEveryEntryLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var a = dictionary.Add(1, "a");
        var b = dictionary.Add(2, "b");
        var countAtEnd = -1;
        a.Listen(() => countAtEnd = dictionary.Count);

        dictionary.Dispose();

        a.IsTerminated.Should().BeTrue();
        b.IsTerminated.Should().BeTrue();
        countAtEnd.Should().Be(0);
        dictionary.Should().BeEmpty();
    }

    [Fact]
    public void Dictionary_Advise_SeesExistingAndAdded()
    {
        var dictionary = new ViewableDictionary<int, string>();
        dictionary.Add(1, "a");
        var seen = new List<(int, string)>();

        dictionary.Advise(new Lifetime(), (key, value) => seen.Add((key, value)));
        dictionary.Add(2, "b");

        seen.Should().Equal((1, "a"), (2, "b"));
    }

    // F2.43: aligned with the client copy - an equal value is not a change.
    [Fact]
    public void Property_SetEqual_DoesNotNotify_KeepsValueLifetime()
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

    [Fact]
    public void Property_SetDifferent_NotifiesAndRenewsValueLifetime()
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

    [Fact]
    public void Property_InvokeAdvices_NotifiesWithCurrentValue()
    {
        var property = new ViewableProperty<int>(1);
        var seen = new List<int>();

        property.Advise(new Lifetime(), (_, value) => seen.Add(value));
        property.InvokeAdvices();

        seen.Should().Equal(1);
    }

    [Fact]
    public void Property_Dispose_EndsValueLifetime_DropsListeners()
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
    public async Task Property_WaitTrue_CompletesOnTrue()
    {
        var property = new ViewableProperty<bool>(false);

        var wait = property.WaitTrue(new Lifetime());
        wait.IsCompleted.Should().BeFalse();

        property.Set(true);

        await wait;
    }

    [Fact]
    public async Task Property_WaitTrue_LifetimeEnds_IsCancelled()
    {
        var property = new ViewableProperty<bool>(false);
        var lifetime = new Lifetime();

        var wait = property.WaitTrue(lifetime);
        lifetime.Terminate();

        await FluentActions.Awaiting(() => wait).Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public void EventSource_SameHandlerTwice_EndOfOneLifetimeRemovesOnlyItsSubscription()
    {
        var source = new EventSource();
        var first = new Lifetime();
        var calls = 0;
        Action handler = () => calls++;

        source.Advise(first, handler);
        source.Advise(new Lifetime(), handler);
        first.Terminate();
        source.Invoke();

        calls.Should().Be(1);
    }

    // F2.43: every Advise is its own entry - the end of a lifetime removes exactly its subscription, not the first
    // one with an equal handler (that reordered the listeners).
    [Fact]
    public void EventSource_SameHandlerTwice_EndOfLaterLifetimeKeepsOrder()
    {
        var source = new EventSource();
        var later = new Lifetime();
        var order = new List<string>();
        Action handler = () => order.Add("handler");

        source.Advise(new Lifetime(), handler);
        source.Advise(new Lifetime(), () => order.Add("other"));
        source.Advise(later, handler);
        later.Terminate();
        source.Invoke();

        order.Should().Equal("handler", "other");
    }

    [Fact]
    public void EventSource_AdviseNull_Throws()
    {
        var source = new EventSource();

        var act = () => source.Advise(new Lifetime(), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
