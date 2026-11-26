using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

/// <summary>
/// Entries of the viewable collections: their lifetimes and events.
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
    public void List_Insert_GivesEntryLifetime_AtIndex()
    {
        var list = new ViewableList<string>();
        list.Add("a");
        list.Add("c");

        var lifetime = list.Insert(1, "b");

        list.Should().Equal("a", "b", "c");
        list.GetLifetime("b").Should().BeSameAs(lifetime);
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

    // As in Remove, the entries leave first: a listener of an entry lifetime sees the list empty and may add again.
    [Fact]
    public void List_Clear_EndsEntryLifetimesAfterEmptying_ListenerMayAdd()
    {
        var list = new ViewableList<string>();
        var a = list.Add("a");
        var b = list.Add("b");
        var countAtEnd = -1;

        a.Listen(() => {
            countAtEnd = list.Count;
            list.Add("re-added");
        });

        list.Clear();

        a.IsTerminated.Should().BeTrue();
        b.IsTerminated.Should().BeTrue();
        countAtEnd.Should().Be(0);
        list.Should().Equal("re-added");
        list.GetLifetime("re-added").IsTerminated.Should().BeFalse();
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

    [Fact]
    public void Dictionary_IndexerSet_ReplacesValue_RenewsEntryLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var first = dictionary.Add(1, "a");
        var seen = new List<string>();
        dictionary.Advise(new Lifetime(), (_, _, value) => seen.Add(value));

        dictionary.Set(1, "b");
        dictionary.Set(2, "c");

        first.IsTerminated.Should().BeTrue();
        dictionary[1].Should().Be("b");
        dictionary.GetLifetime(1).IsTerminated.Should().BeFalse();
        seen.Should().Equal("b", "c");
    }

    // Dispose ends the entry lifetimes, as Remove does for one entry.
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

    // Clear ends the entry lifetimes as Remove does, after the entries left: the listener sees it empty.
    [Fact]
    public void Dictionary_Clear_EndsEveryEntryLifetime_AfterEmptying()
    {
        var dictionary = new CountingDictionary();
        var a = dictionary.Add(1, "a");
        var b = dictionary.Add(2, "b");
        var countAtEnd = -1;
        a.Listen(() => countAtEnd = dictionary.Count);
        var modifiedBefore = dictionary.Modified;

        dictionary.Clear();

        a.IsTerminated.Should().BeTrue();
        b.IsTerminated.Should().BeTrue();
        countAtEnd.Should().Be(0);
        dictionary.Should().BeEmpty();
        dictionary.Modified.Should().Be(modifiedBefore + 1);
    }

    [Fact]
    public void Dictionary_ClearedKeyAddedAgain_GetsNewLifetime_ListenerMayAdd()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var first = dictionary.Add(1, "a");
        first.Listen(() => dictionary.Add(2, "re-added"));

        dictionary.Clear();
        var second = dictionary.Add(1, "b");

        first.IsTerminated.Should().BeTrue();
        second.IsTerminated.Should().BeFalse();
        dictionary.GetLifetime(1).Should().BeSameAs(second);
        dictionary.GetLifetime(2).IsTerminated.Should().BeFalse();
        dictionary.Count.Should().Be(2);
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

    [Fact]
    public void Dictionary_AddLifetimed_RemovesWithTheLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var lifetime = new Lifetime();

        dictionary.AddLifetimed(lifetime, 1, "a");
        lifetime.Terminate();

        dictionary.Should().BeEmpty();
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

    private class CountingDictionary : ViewableDictionary<int, string>
    {
        public int Modified { get; private set; }

        protected override void OnModified()
        {
            Modified++;
        }
    }

    [Fact]
    public void List_AddDuplicate_ThrowsWithoutChangingTheList()
    {
        var list = new ViewableList<string>();
        list.Add("a");

        var add = () => list.Add("a");

        add.Should().Throw<ArgumentException>();
        list.Count.Should().Be(1);
    }

    [Fact]
    public void List_InsertDuplicate_ThrowsWithoutChangingTheList()
    {
        var list = new ViewableList<string>();
        list.Add("a");

        var insert = () => list.Insert(0, "a");

        insert.Should().Throw<ArgumentException>();
        list.Count.Should().Be(1);
    }

    // The base List / Dictionary mutators change the entries without their lifetimes: they must not be reachable.
    [Fact]
    public void List_IsNotAMutableCollection()
    {
        object list = new ViewableList<string>();

        (list is ICollection<string>).Should().BeFalse();
        (list is System.Collections.IList).Should().BeFalse();
    }

    [Fact]
    public void Dictionary_IsNotAMutableDictionary()
    {
        object dictionary = new ViewableDictionary<int, string>();

        (dictionary is ICollection<KeyValuePair<int, string>>).Should().BeFalse();
        (dictionary is System.Collections.IDictionary).Should().BeFalse();
    }

    [Fact]
    public void List_KeepsTheReadMembersTheGamesUse()
    {
        var list = new ViewableList<string>();
        list.Add("a");
        list.Add("b");

        list.Contains("b").Should().BeTrue();
        list.IndexOf("b").Should().Be(1);
        list[0].Should().Be("a");

        list.RemoveAt(0);

        list.Should().Equal("b");
    }

    [Fact]
    public void Dictionary_KeepsTheReadMembersTheGamesUse()
    {
        var dictionary = new ViewableDictionary<int, string>();
        dictionary.Add(1, "a");

        dictionary.ContainsKey(1).Should().BeTrue();
        dictionary.TryGetValue(1, out var value).Should().BeTrue();
        value.Should().Be("a");
        dictionary.Values.Should().Equal("a");
        dictionary.Keys.Should().Equal(1);
        dictionary[1].Should().Be("a");
        dictionary.Count.Should().Be(1);
    }

    [Fact]
    public void Dictionary_IndexerSetOfNewKey_ReportsOneModification()
    {
        var dictionary = new CountingDictionary();

        dictionary.Set(1, "a");

        dictionary.Modified.Should().Be(1);
    }
}
