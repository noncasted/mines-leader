using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class EventSourceTests
    {
        [Test]
        public void Invoke_DeliversToAllHandlers()
        {
            var lifetime = new Lifetime();
            var source = new EventSource<int>();
            var received = new List<int>();

            source.Advise(lifetime, v => received.Add(v));
            source.Advise(lifetime, v => received.Add(v * 10));

            source.Invoke(3);

            CollectionAssert.AreEqual(new[] { 3, 30 }, received);
        }

        [Test]
        public void Invoke_HandlerThrows_OthersStillReceive()
        {
            var lifetime = new Lifetime();
            var source = new EventSource();
            var secondCalled = false;

            source.Advise(lifetime, () => throw new InvalidOperationException("boom"));
            source.Advise(lifetime, () => secondCalled = true);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));

            source.Invoke();

            Assert.IsTrue(secondCalled);
        }

        [Test]
        public void Invoke_HandlerThrows_SubscriptionsAfterwardsStillWork()
        {
            var lifetime = new Lifetime();
            var source = new EventSource();
            var lateCalled = false;

            source.Advise(lifetime, () => throw new InvalidOperationException("boom"));

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            source.Invoke();

            source.Advise(lifetime, () => lateCalled = true);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            source.Invoke();

            Assert.IsTrue(lateCalled);
        }

        [Test]
        public void LifetimeTerminated_HandlerIsRemoved()
        {
            var lifetime = new Lifetime();
            var source = new EventSource();
            var calls = 0;

            source.Advise(lifetime, () => calls++);

            source.Invoke();
            lifetime.Terminate();
            source.Invoke();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void LifetimeTerminated_DuringInvoke_HandlerStillRemovedAfterwards()
        {
            var outer = new Lifetime();
            var inner = new Lifetime();
            var source = new EventSource();
            var innerCalls = 0;

            source.Advise(outer, () => inner.Terminate());
            source.Advise(inner, () => innerCalls++);

            source.Invoke();
            source.Invoke();

            Assert.AreEqual(1, innerCalls);
        }

        [Test]
        public void Advise_DuringInvoke_ReceivesFromNextInvoke()
        {
            var lifetime = new Lifetime();
            var source = new EventSource();
            var lateCalls = 0;
            var subscribed = false;

            source.Advise(lifetime, () => {
                if (subscribed == true)
                    return;

                subscribed = true;
                source.Advise(lifetime, () => lateCalls++);
            });

            source.Invoke();
            Assert.AreEqual(0, lateCalls);

            source.Invoke();
            Assert.AreEqual(1, lateCalls);
        }
    }
}
