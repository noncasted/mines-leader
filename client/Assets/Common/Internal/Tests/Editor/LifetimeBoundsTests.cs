using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.Tests
{
    // F2.42: a TerminatedLifetime behaves as an ended Lifetime (listeners run at once, the token is cancelled), and the
    // value/entry lifetime handed out by View is bounded by the subscriber lifetime.
    public class LifetimeBoundsTests
    {
        [Test]
        public void TerminatedLifetime_Listen_InvokesImmediately()
        {
            var lifetime = new TerminatedLifetime();
            var calls = 0;

            lifetime.Listen(() => calls++);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void TerminatedLifetime_Token_IsCancelled()
        {
            Assert.IsTrue(new TerminatedLifetime().Token.IsCancellationRequested);
        }

        [Test]
        public void TerminatedLifetime_ListenNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TerminatedLifetime().Listen(null));
        }

        [Test]
        public void TerminatedLifetime_Advise_DoesNotDeliver()
        {
            var source = new EventSource<int>();
            var calls = 0;

            source.Advise(new TerminatedLifetime(), _ => calls++);
            source.Invoke(1);

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void TerminatedLifetime_Child_IsTerminated()
        {
            Assert.IsTrue(new TerminatedLifetime().Child().IsTerminated);
        }

        [Test]
        public void PropertyView_ValueLifetime_EndsWithSubscriber()
        {
            var property = new ViewableProperty<int>(1);
            var subscriber = new Lifetime();
            IReadOnlyLifetime viewed = null;

            property.View(subscriber, (valueLifetime, _) => viewed = valueLifetime);

            Assert.IsFalse(viewed.IsTerminated);

            subscriber.Terminate();

            Assert.IsTrue(viewed.IsTerminated);
            Assert.IsFalse(property.ValueLifetime.IsTerminated);
        }

        [Test]
        public void PropertyView_ValueLifetime_EndsWithValue()
        {
            var property = new ViewableProperty<int>(1);
            var subscriber = new Lifetime();
            var viewed = new List<IReadOnlyLifetime>();

            property.View(subscriber, (valueLifetime, _) => viewed.Add(valueLifetime));
            property.Set(2);

            Assert.AreEqual(2, viewed.Count);
            Assert.IsTrue(viewed[0].IsTerminated);
            Assert.IsFalse(viewed[1].IsTerminated);
        }

        [Test]
        public void PropertyView_SubscriptionMadeInValueLifetime_EndsWithSubscriber()
        {
            var property = new ViewableProperty<int>(1);
            var inner = new EventSource<int>();
            var subscriber = new Lifetime();
            var calls = 0;

            property.View(subscriber, (valueLifetime, _) => inner.Advise(valueLifetime, _ => calls++));
            subscriber.Terminate();
            inner.Invoke(1);

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void PropertyView_ManyChanges_LeaveNoListenerOnSubscriber()
        {
            var property = new ViewableProperty<int>(0);
            var subscriber = new CountingLifetime();

            property.View(subscriber, (_, _) => { });

            for (var i = 1; i <= 10; i++)
                property.Set(i);

            // The subscription itself and the intersection of the current value: earlier intersections are gone.
            Assert.AreEqual(2, subscriber.Listeners);
        }

        [Test]
        public void PropertyViewNotNull_ValueLifetime_EndsWithSubscriber()
        {
            var property = new ViewableProperty<string>("a");
            var subscriber = new Lifetime();
            IReadOnlyLifetime viewed = null;

            property.ViewNotNull(subscriber, (valueLifetime, _) => viewed = valueLifetime);
            subscriber.Terminate();

            Assert.IsTrue(viewed.IsTerminated);
        }

        [Test]
        public void PropertyAdviseTrue_ValueLifetime_EndsWithSubscriber()
        {
            var property = new ViewableProperty<bool>(false);
            var subscriber = new Lifetime();
            IReadOnlyLifetime viewed = null;

            property.AdviseTrue(subscriber, (IReadOnlyLifetime valueLifetime) => viewed = valueLifetime);
            property.Set(true);
            subscriber.Terminate();

            Assert.IsTrue(viewed.IsTerminated);
            Assert.IsFalse(property.ValueLifetime.IsTerminated);
        }

        [Test]
        public void ListView_EntryLifetime_EndsWithSubscriber_ListEntryStays()
        {
            var list = new ViewableList<string>();
            list.Add("existing");
            var subscriber = new Lifetime();
            var viewed = new Dictionary<string, IReadOnlyLifetime>();

            list.View(subscriber, (entryLifetime, value) => viewed[value] = entryLifetime);
            list.Add("added");
            subscriber.Terminate();

            Assert.IsTrue(viewed["existing"].IsTerminated);
            Assert.IsTrue(viewed["added"].IsTerminated);
            Assert.IsFalse(list.GetLifetime("existing").IsTerminated);
            Assert.IsFalse(list.GetLifetime("added").IsTerminated);
        }

        [Test]
        public void ListView_EntryLifetime_EndsWithRemove()
        {
            var list = new ViewableList<string>();
            var subscriber = new Lifetime();
            IReadOnlyLifetime viewed = null;

            list.View(subscriber, (entryLifetime, _) => viewed = entryLifetime);
            list.Add("a");
            list.Remove("a");

            Assert.IsTrue(viewed.IsTerminated);
            Assert.IsFalse(subscriber.IsTerminated);
        }

        [Test]
        public void ListAdd_AfterDispose_ReturnsLifetimeThatRunsListenersAtOnce()
        {
            var list = new ViewableList<string>();
            list.Dispose();
            var called = false;

            list.Add("a").Listen(() => called = true);

            Assert.IsTrue(called);
        }

        [Test]
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

            Assert.AreEqual(4, viewed.Count);

            foreach (var lifetime in viewed)
                Assert.IsTrue(lifetime.IsTerminated);

            Assert.IsFalse(dictionary.GetLifetime(1).IsTerminated);
            Assert.IsFalse(dictionary.GetLifetime(2).IsTerminated);
        }

        [Test]
        public void Dictionary_RemovedKeyAddedAgain_GetsNewLifetime()
        {
            var dictionary = new ViewableDictionary<int, string>();
            var first = dictionary.Add(1, "a");
            dictionary.Remove(1);

            var second = dictionary.Add(1, "b");

            Assert.IsTrue(first.IsTerminated);
            Assert.IsFalse(second.IsTerminated);
            Assert.AreSame(second, dictionary.GetLifetime(1));
        }

        [Test]
        public void Dictionary_RemoveUnknown_Throws()
        {
            var dictionary = new ViewableDictionary<int, string>();

            Assert.Throws<KeyNotFoundException>(() => dictionary.Remove(1));
        }

        // F2.43: Dispose ends the entry lifetimes, as Remove does for one entry.
        [Test]
        public void Dictionary_Dispose_EndsEveryEntryLifetime()
        {
            var dictionary = new ViewableDictionary<int, string>();
            var a = dictionary.Add(1, "a");
            var b = dictionary.Add(2, "b");
            var countAtEnd = -1;
            a.Listen(() => countAtEnd = dictionary.Count);

            dictionary.Dispose();

            Assert.IsTrue(a.IsTerminated);
            Assert.IsTrue(b.IsTerminated);
            Assert.AreEqual(0, countAtEnd);
            Assert.AreEqual(0, dictionary.Count);
        }

        // Counts the live listeners: shows whether the intersections leave the subscriber lifetime.
        private class CountingLifetime : IReadOnlyLifetime
        {
            private readonly Lifetime _inner = new();

            public int Listeners { get; private set; }

            public System.Threading.CancellationToken Token => _inner.Token;
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
}
